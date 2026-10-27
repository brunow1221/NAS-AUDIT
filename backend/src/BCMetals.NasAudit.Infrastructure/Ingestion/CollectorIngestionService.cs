using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Application.Ingestion;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCMetals.NasAudit.Infrastructure.Ingestion;

public sealed class CollectorIngestionService(
    AuditDbContext dbContext,
    ISambaMessageParser parser,
    ILiveEventPublisher publisher,
    IConfiguration? configuration = null) : ICollectorIngestionService
{
    private static readonly Regex CollectorIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._-]{2,127}$", RegexOptions.Compiled);
    private const int MaxEventsPerBatch = 200;

    public async Task<IngestionBatchResult> IngestAsync(IngestionBatchRequest request, CancellationToken cancellationToken)
    {
        ValidateBatch(request);
        var prior = await dbContext.IngestionBatches.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CollectorId == request.CollectorId && x.BatchId == request.BatchId, cancellationToken);
        if (prior is not null) return Result(prior, true);

        var sourceName = $"Journal:{request.CollectorId}";
        var source = await dbContext.EventSources.SingleOrDefaultAsync(x => x.Name == sourceName, cancellationToken);
        if (source is null)
        {
            source = new EventSource { Name = sourceName, Kind = "Journal" };
            dbContext.EventSources.Add(source);
        }

        var checkpoint = await dbContext.CollectorCheckpoints
            .SingleOrDefaultAsync(x => x.CollectorId == request.CollectorId, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new CollectorCheckpoint { CollectorId = request.CollectorId, EventSource = source, EventSourceId = source.Id };
            dbContext.CollectorCheckpoints.Add(checkpoint);
        }

        var candidates = new List<(CollectorEventRequest Request, AuditEvent Event)>();
        var rejectedCandidates = new List<RejectedIngestionEvent>();
        var rejected = 0;
        string? confirmedCursor = checkpoint.Cursor;
        string? confirmedBootId = checkpoint.BootId;
        DateTimeOffset? confirmedEventUtc = checkpoint.LastEventUtc;

        foreach (var item in request.Events)
        {
            confirmedCursor = item.Cursor;
            confirmedBootId = item.BootId;
            confirmedEventUtc = item.TimestampUtc.ToUniversalTime();
            if (!TryCreateEvent(request.CollectorId, source, item, out var auditEvent))
            {
                rejected++;
                rejectedCandidates.Add(new RejectedIngestionEvent
                {
                    CollectorId = request.CollectorId,
                    BatchId = request.BatchId,
                    Cursor = item.Cursor,
                    BootId = Truncate(item.BootId, 80),
                    EventTimestampUtc = item.TimestampUtc.ToUniversalTime(),
                    RawMessage = item.Message,
                    Reason = "unsupported_or_malformed_samba_message",
                    MetadataJson = JsonSerializer.Serialize(item.Metadata ?? new Dictionary<string, JsonElement>()),
                    DeduplicationKey = Sha256($"journal-rejected:{request.CollectorId}:{item.BootId}:{item.Cursor}")
                });
                continue;
            }
            candidates.Add((item, auditEvent!));
        }

        var keys = candidates.Select(x => x.Event.DeduplicationKey).ToList();
        var existing = await dbContext.AuditEvents.AsNoTracking()
            .Where(x => keys.Contains(x.DeduplicationKey))
            .Select(x => x.DeduplicationKey)
            .ToHashSetAsync(cancellationToken);
        var additions = candidates.Where(x => !existing.Contains(x.Event.DeduplicationKey)).Select(x => x.Event).ToList();
        dbContext.AuditEvents.AddRange(additions);

        var rejectedKeys = rejectedCandidates.Select(x => x.DeduplicationKey).ToList();
        var existingRejected = await dbContext.RejectedIngestionEvents.AsNoTracking()
            .Where(x => rejectedKeys.Contains(x.DeduplicationKey))
            .Select(x => x.DeduplicationKey)
            .ToHashSetAsync(cancellationToken);
        dbContext.RejectedIngestionEvents.AddRange(
            rejectedCandidates.Where(x => !existingRejected.Contains(x.DeduplicationKey)));

        checkpoint.Cursor = confirmedCursor;
        checkpoint.BootId = confirmedBootId;
        checkpoint.LastEventUtc = confirmedEventUtc;
        checkpoint.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var batch = new IngestionBatch
        {
            CollectorId = request.CollectorId,
            BatchId = request.BatchId,
            Accepted = additions.Count,
            Duplicates = candidates.Count - additions.Count,
            Rejected = rejected,
            ConfirmedCursor = confirmedCursor
        };
        dbContext.IngestionBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var auditEvent in additions)
            await publisher.PublishAsync(Map(auditEvent, source), CancellationToken.None);
        return Result(batch, false);
    }

    public async Task RecordHeartbeatAsync(CollectorHeartbeatRequest request, CancellationToken cancellationToken)
    {
        ValidateCollectorId(request.CollectorId);
        if (request.QueueDepth < 0) throw new ArgumentException("La profundidad de cola no puede ser negativa.");
        var allowed = new[] { "healthy", "source_unavailable", "recovering", "error" };
        var status = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!allowed.Contains(status, StringComparer.Ordinal))
            throw new ArgumentException("Estado de recolector no reconocido.");
        var observed = request.ObservedAtUtc.ToUniversalTime();
        if (observed > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new ArgumentException("La marca de tiempo del heartbeat está en el futuro.");

        var heartbeat = await dbContext.CollectorHeartbeats
            .SingleOrDefaultAsync(x => x.CollectorId == request.CollectorId, cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new CollectorHeartbeat { CollectorId = request.CollectorId, Status = status };
            dbContext.CollectorHeartbeats.Add(heartbeat);
        }
        heartbeat.Status = status;
        heartbeat.ObservedAtUtc = observed;
        heartbeat.LastEventUtc = request.LastEventUtc?.ToUniversalTime();
        heartbeat.QueueDepth = request.QueueDepth;
        heartbeat.Message = Truncate(request.Message, 2000);
        heartbeat.MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, JsonElement>());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CollectorStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var token = configuration?["Collector:Token"];
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            return new CollectorStatusDto("not_configured", "Backend funcionando sin agente", null, null, null, null, 0, null);

        var heartbeat = await dbContext.CollectorHeartbeats.AsNoTracking()
            .OrderByDescending(x => x.ObservedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (heartbeat is null)
            return new CollectorStatusDto("backend_without_agent", "Backend funcionando sin agente", null, null, null, null, 0, null);
        var checkpoint = await dbContext.CollectorCheckpoints.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CollectorId == heartbeat.CollectorId, cancellationToken);
        if (heartbeat.ObservedAtUtc < DateTimeOffset.UtcNow.AddSeconds(-90))
            return Status("disconnected", "Recolector desconectado", heartbeat, checkpoint);
        return heartbeat.Status switch
        {
            "source_unavailable" => Status("source_unavailable", "Fuente journald inaccesible", heartbeat, checkpoint),
            "recovering" => Status("recovering", "Recuperando eventos pendientes", heartbeat, checkpoint),
            "error" => Status("error", "Recolector con errores", heartbeat, checkpoint),
            _ when heartbeat.LastEventUtc is null => Status("active_no_events", "Monitoreo activo — sin eventos nuevos", heartbeat, checkpoint),
            _ => Status("connected", "Recolector conectado", heartbeat, checkpoint)
        };
    }

    private bool TryCreateEvent(string collectorId, EventSource source, CollectorEventRequest item, out AuditEvent? auditEvent)
    {
        auditEvent = null;
        if (string.IsNullOrWhiteSpace(item.Cursor) || item.Cursor.Length > 2048 ||
            string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 65_536 ||
            item.TimestampUtc == default || item.TimestampUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
            !parser.TryParse(item.Message, out var parsed) || parsed is null) return false;
        var path = parsed.DestinationPath ?? parsed.FilePath ?? parsed.SourcePath;
        var key = Sha256($"journal:{collectorId}:{item.BootId}:{item.Cursor}");
        auditEvent = new AuditEvent
        {
            EventTimestampUtc = item.TimestampUtc.ToUniversalTime(),
            SmbUser = parsed.SmbUser,
            ClientIp = parsed.ClientIp,
            Operation = parsed.Operation,
            InterpretedAction = parsed.Action,
            InterpretationConfidence = parsed.Confidence,
            OperationResult = parsed.Result,
            ShareName = parsed.ShareName,
            FilePath = parsed.FilePath ?? parsed.DestinationPath,
            SourcePath = parsed.SourcePath,
            DestinationPath = parsed.DestinationPath,
            FileExtension = ExtensionOf(path),
            ObjectType = parsed.ObjectType,
            IsAuxiliaryStream = parsed.IsAuxiliaryStream,
            IsRecyclePath = parsed.IsRecyclePath,
            RawMessage = item.Message,
            EventSource = source,
            EventSourceId = source.Id,
            JournalCursor = item.Cursor,
            BootId = Truncate(item.BootId, 80),
            DeduplicationKey = key,
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Live,
            MetadataJson = JsonSerializer.Serialize(item.Metadata ?? new Dictionary<string, JsonElement>())
        };
        return true;
    }

    private static void ValidateBatch(IngestionBatchRequest request)
    {
        ValidateCollectorId(request.CollectorId);
        if (request.BatchId == Guid.Empty) throw new ArgumentException("batchId es obligatorio.");
        if (request.Events is null || request.Events.Count is < 1 or > MaxEventsPerBatch)
            throw new ArgumentException($"Cada lote debe contener entre 1 y {MaxEventsPerBatch} eventos.");
        foreach (var item in request.Events)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Cursor) || item.Cursor.Length > 2048)
                throw new ArgumentException("Cada evento debe incluir un cursor journald válido.");
            if (string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 65_536)
                throw new ArgumentException("Cada evento debe incluir un mensaje dentro del límite permitido.");
            if (item.TimestampUtc == default || item.TimestampUtc > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new ArgumentException("Cada evento debe incluir una marca de tiempo válida.");
        }
    }

    private static void ValidateCollectorId(string collectorId)
    {
        if (string.IsNullOrWhiteSpace(collectorId) || !CollectorIdPattern.IsMatch(collectorId))
            throw new ArgumentException("collectorId no cumple el formato permitido.");
    }

    private static CollectorStatusDto Status(string status, string label, CollectorHeartbeat heartbeat, CollectorCheckpoint? checkpoint) =>
        new(status, label, heartbeat.CollectorId, heartbeat.ObservedAtUtc, heartbeat.LastEventUtc,
            checkpoint?.Cursor, heartbeat.QueueDepth, heartbeat.Message);

    private static IngestionBatchResult Result(IngestionBatch batch, bool replay) =>
        new(batch.BatchId, batch.Accepted, batch.Duplicates, batch.Rejected, batch.ConfirmedCursor, replay);

    private static string? ExtensionOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var extension = Path.GetExtension(path.Split(':')[0]);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.ToLowerInvariant();
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maxLength)];

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static AuditEventDto Map(AuditEvent x, EventSource source) => new(
        x.Id, x.EventTimestampUtc, x.SmbUser, x.ClientIp, x.Operation, x.InterpretedAction,
        x.InterpretationConfidence, x.OperationResult, x.ShareName, x.FilePath, x.SourcePath,
        x.DestinationPath, x.FileExtension, x.ObjectType, x.IsAuxiliaryStream, x.IsRecyclePath,
        x.RawMessage, source.Name, source.Kind, x.JournalCursor, x.BootId, x.MetadataJson,
        x.DataMode, x.ParserVersion);
}
