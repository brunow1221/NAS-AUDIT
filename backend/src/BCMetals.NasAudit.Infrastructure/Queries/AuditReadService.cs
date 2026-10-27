using System.Globalization;
using System.Text;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Dashboard;
using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using CsvHelper;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Infrastructure.Queries;

public sealed class AuditReadService(AuditDbContext dbContext) : IAuditReadService
{
    private const int MaxExportRows = 100_000;

    public async Task<PagedResult<AuditEventDto>> GetEventsAsync(EventQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var events = ApplyFilters(dbContext.AuditEvents.AsNoTracking(), query);
        var total = await events.LongCountAsync(cancellationToken);
        var rows = await ApplySort(events.Include(x => x.EventSource), query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditEventDto>(
            rows.Select(Map).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<AuditEventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await dbContext.AuditEvents.AsNoTracking()
            .Include(x => x.EventSource)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<AuditEventDto>> GetRecentLiveEventsAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await dbContext.AuditEvents.AsNoTracking().Include(x => x.EventSource)
            .Where(x => x.DataMode == DataMode.Live)
            .OrderByDescending(x => x.EventTimestampUtc).ThenByDescending(x => x.ReceivedAtUtc)
            .Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        rows.Reverse();
        return rows.Select(Map).ToList();
    }

    public async Task<EventExport> ExportEventsAsync(EventQuery query, CancellationToken cancellationToken)
    {
        var events = ApplyFilters(dbContext.AuditEvents.AsNoTracking(), query);
        var count = await events.LongCountAsync(cancellationToken);
        if (count > MaxExportRows)
        {
            throw new InvalidDataException(
                $"La consulta contiene {count:N0} eventos. Acote los filtros a un máximo de {MaxExportRows:N0} filas por exportación.");
        }

        var rows = await ApplySort(events.Include(x => x.EventSource), query).ToListAsync(cancellationToken);
        using var stream = new MemoryStream();
        await stream.WriteAsync(Encoding.UTF8.GetPreamble(), cancellationToken);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            var headers = new[]
            {
                "Id", "Fecha UTC", "Cuenta SMB", "IP", "Acción interpretada", "Operación técnica",
                "Certeza", "Resultado", "Recurso", "Archivo/Ruta", "Ruta original", "Ruta destino",
                "Extensión", "Tipo de objeto", "Flujo auxiliar", "Papelera", "Origen", "Tipo de origen",
                "Versión parser", "Mensaje original"
            };
            foreach (var header in headers) csv.WriteField(header);
            await csv.NextRecordAsync();

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                csv.WriteField(row.Id);
                csv.WriteField(row.EventTimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(SafeCsv(row.SmbUser));
                csv.WriteField(SafeCsv(row.ClientIp));
                csv.WriteField(row.InterpretedAction.ToString());
                csv.WriteField(SafeCsv(row.Operation));
                csv.WriteField(row.InterpretationConfidence.ToString());
                csv.WriteField(SafeCsv(row.OperationResult));
                csv.WriteField(SafeCsv(row.ShareName));
                csv.WriteField(SafeCsv(row.FilePath));
                csv.WriteField(SafeCsv(row.SourcePath));
                csv.WriteField(SafeCsv(row.DestinationPath));
                csv.WriteField(SafeCsv(row.FileExtension));
                csv.WriteField(SafeCsv(row.ObjectType));
                csv.WriteField(row.IsAuxiliaryStream ? "Sí" : "No");
                csv.WriteField(row.IsRecyclePath ? "Sí" : "No");
                csv.WriteField(SafeCsv(row.EventSource?.Name));
                csv.WriteField(SafeCsv(row.EventSource?.Kind));
                csv.WriteField(SafeCsv(row.ParserVersion));
                csv.WriteField(SafeCsv(row.RawMessage));
                await csv.NextRecordAsync();
            }
            writer.Flush();
        }

        var fileName = $"bcmetals-auditoria-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new EventExport(stream.ToArray(), fileName, count);
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var allMode = dbContext.AuditEvents.AsNoTracking().Where(x => x.DataMode == query.DataMode && !x.IsAuxiliaryStream);
        var period = allMode.Where(x => x.EventTimestampUtc >= query.FromUtc && x.EventTimestampUtc < query.ToUtc);
        var last24 = DateTimeOffset.UtcNow.AddHours(-24);
        var heartbeat = await dbContext.CollectorHeartbeats.AsNoTracking()
            .OrderByDescending(x => x.ObservedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var collectorStatus = heartbeat is null ? "backend_without_agent"
            : heartbeat.ObservedAtUtc < DateTimeOffset.UtcNow.AddSeconds(-90) ? "disconnected"
            : heartbeat.Status;
        return new DashboardSummaryDto(
            await allMode.LongCountAsync(cancellationToken),
            await allMode.LongCountAsync(x => x.EventTimestampUtc >= last24, cancellationToken),
            await period.Where(x => x.SmbUser != null).Select(x => x.SmbUser).Distinct().CountAsync(cancellationToken),
            await period.LongCountAsync(x => x.InterpretedAction == AuditAction.Creation, cancellationToken),
            await period.LongCountAsync(x => x.InterpretedAction == AuditAction.Write, cancellationToken),
            await period.LongCountAsync(x => x.InterpretedAction == AuditAction.Deletion, cancellationToken),
            await period.LongCountAsync(x => x.InterpretedAction == AuditAction.Rename, cancellationToken),
            await period.LongCountAsync(x => x.InterpretedAction == AuditAction.Connection, cancellationToken),
            await allMode.MaxAsync(x => (DateTimeOffset?)x.EventTimestampUtc, cancellationToken),
            collectorStatus);
    }

    public async Task<IReadOnlyList<ActivityPointDto>> GetActivityAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var source = InPeriod(query);
        if (query.ToUtc - query.FromUtc <= TimeSpan.FromDays(2))
        {
            var rows = await source.GroupBy(x => new { x.EventTimestampUtc.Year, x.EventTimestampUtc.Month, x.EventTimestampUtc.Day, x.EventTimestampUtc.Hour })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.LongCount() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ThenBy(x => x.Hour)
                .ToListAsync(cancellationToken);
            return rows.Select(x => new ActivityPointDto(new DateTimeOffset(x.Year, x.Month, x.Day, x.Hour, 0, 0, TimeSpan.Zero), x.Count)).ToList();
        }

        var days = await source.GroupBy(x => new { x.EventTimestampUtc.Year, x.EventTimestampUtc.Month, x.EventTimestampUtc.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.LongCount() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
            .ToListAsync(cancellationToken);
        return days.Select(x => new ActivityPointDto(new DateTimeOffset(x.Year, x.Month, x.Day, 0, 0, 0, TimeSpan.Zero), x.Count)).ToList();
    }

    public async Task<IReadOnlyList<DistributionPointDto>> GetDistributionAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var rows = await InPeriod(query).GroupBy(x => x.InterpretedAction)
            .Select(g => new { Action = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count).ToListAsync(cancellationToken);
        return rows.Select(x => new DistributionPointDto(x.Action, x.Count)).ToList();
    }

    public async Task<IReadOnlyList<TopUserDto>> GetTopUsersAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var rows = await InPeriod(query).Where(x => x.SmbUser != null && x.SmbUser != string.Empty)
            .GroupBy(x => x.SmbUser!).Select(g => new { User = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync(cancellationToken);
        return rows.Select(x => new TopUserDto(x.User, x.Count)).ToList();
    }

    public async Task<IReadOnlyList<ShareActivityDto>> GetSharesAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var rows = await InPeriod(query).Where(x => x.ShareName != null && x.ShareName != string.Empty)
            .GroupBy(x => x.ShareName!).Select(g => new { Share = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(cancellationToken);
        return rows.Select(x => new ShareActivityDto(x.Share, x.Count)).ToList();
    }

    private IQueryable<AuditEvent> InPeriod(DashboardQuery query) =>
        dbContext.AuditEvents.AsNoTracking().Where(x =>
            x.DataMode == query.DataMode && !x.IsAuxiliaryStream &&
            x.EventTimestampUtc >= query.FromUtc && x.EventTimestampUtc < query.ToUtc);

    private static IQueryable<AuditEvent> ApplyFilters(IQueryable<AuditEvent> source, EventQuery query)
    {
        source = source.Where(x => x.DataMode == query.DataMode);
        if (query.FromUtc is not null) source = source.Where(x => x.EventTimestampUtc >= query.FromUtc);
        if (query.ToUtc is not null) source = source.Where(x => x.EventTimestampUtc < query.ToUtc);
        if (!string.IsNullOrWhiteSpace(query.User)) source = source.Where(x => x.SmbUser == query.User.Trim());
        if (!string.IsNullOrWhiteSpace(query.ClientIp)) source = source.Where(x => x.ClientIp == query.ClientIp.Trim());
        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            var operation = query.Operation.Trim().ToLowerInvariant();
            source = source.Where(x => x.Operation == operation);
        }
        if (query.Action is not null) source = source.Where(x => x.InterpretedAction == query.Action);
        if (!string.IsNullOrWhiteSpace(query.Share)) source = source.Where(x => x.ShareName == query.Share.Trim());
        if (!string.IsNullOrWhiteSpace(query.FileName))
        {
            var value = query.FileName.Trim();
            source = source.Where(x => x.FilePath != null && EF.Functions.ILike(x.FilePath, $"%/{value}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            var value = query.Path.Trim();
            source = source.Where(x =>
                (x.FilePath != null && EF.Functions.ILike(x.FilePath, $"%{value}%")) ||
                (x.SourcePath != null && EF.Functions.ILike(x.SourcePath, $"%{value}%")) ||
                (x.DestinationPath != null && EF.Functions.ILike(x.DestinationPath, $"%{value}%")));
        }
        if (!string.IsNullOrWhiteSpace(query.Extension))
        {
            var extension = query.Extension.Trim().ToLowerInvariant();
            if (!extension.StartsWith('.')) extension = $".{extension}";
            source = source.Where(x => x.FileExtension == extension);
        }
        if (!string.IsNullOrWhiteSpace(query.Result)) source = source.Where(x => x.OperationResult == query.Result.Trim());
        if (!string.IsNullOrWhiteSpace(query.Source)) source = source.Where(x => x.EventSource != null && x.EventSource.Name == query.Source.Trim());
        if (query.Confidence is not null) source = source.Where(x => x.InterpretationConfidence == query.Confidence);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(x =>
                (x.FilePath != null && EF.Functions.ILike(x.FilePath, $"%{term}%")) ||
                (x.SourcePath != null && EF.Functions.ILike(x.SourcePath, $"%{term}%")) ||
                (x.DestinationPath != null && EF.Functions.ILike(x.DestinationPath, $"%{term}%")) ||
                (x.SmbUser != null && EF.Functions.ILike(x.SmbUser, $"%{term}%")) ||
                (x.ClientIp != null && EF.Functions.ILike(x.ClientIp, $"%{term}%")) ||
                EF.Functions.ILike(x.RawMessage, $"%{term}%"));
        }
        return source;
    }

    private static IOrderedQueryable<AuditEvent> ApplySort(IQueryable<AuditEvent> source, EventQuery query)
    {
        var descending = query.Descending;
        return query.SortBy.Trim().ToLowerInvariant() switch
        {
            "user" => descending ? source.OrderByDescending(x => x.SmbUser).ThenByDescending(x => x.EventTimestampUtc) : source.OrderBy(x => x.SmbUser).ThenBy(x => x.EventTimestampUtc),
            "action" => descending ? source.OrderByDescending(x => x.InterpretedAction).ThenByDescending(x => x.EventTimestampUtc) : source.OrderBy(x => x.InterpretedAction).ThenBy(x => x.EventTimestampUtc),
            "operation" => descending ? source.OrderByDescending(x => x.Operation).ThenByDescending(x => x.EventTimestampUtc) : source.OrderBy(x => x.Operation).ThenBy(x => x.EventTimestampUtc),
            "result" => descending ? source.OrderByDescending(x => x.OperationResult).ThenByDescending(x => x.EventTimestampUtc) : source.OrderBy(x => x.OperationResult).ThenBy(x => x.EventTimestampUtc),
            _ => descending ? source.OrderByDescending(x => x.EventTimestampUtc).ThenByDescending(x => x.Id) : source.OrderBy(x => x.EventTimestampUtc).ThenBy(x => x.Id)
        };
    }

    internal static string? SafeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var trimmed = value.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] is '=' or '+' or '-' or '@' or '\t' or '\r') ? $"'{value}" : value;
    }

    private static AuditEventDto Map(AuditEvent x) => new(
        x.Id, x.EventTimestampUtc, x.SmbUser, x.ClientIp, x.Operation, x.InterpretedAction,
        x.InterpretationConfidence, x.OperationResult, x.ShareName, x.FilePath, x.SourcePath,
        x.DestinationPath, x.FileExtension, x.ObjectType, x.IsAuxiliaryStream, x.IsRecyclePath,
        x.RawMessage, x.EventSource?.Name ?? "unknown", x.EventSource?.Kind ?? "unknown",
        x.JournalCursor, x.BootId, x.MetadataJson, x.DataMode, x.ParserVersion);
}
