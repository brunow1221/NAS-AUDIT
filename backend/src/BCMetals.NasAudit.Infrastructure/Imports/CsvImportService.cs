using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Importing;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Infrastructure.Importing;

public sealed class CsvImportService(AuditDbContext dbContext, ISambaMessageParser parser) : IImportService
{
    private const int MaxBytes = 20 * 1024 * 1024;
    private const int BatchSize = 500;

    public async Task<CsvPreviewDto> PreviewAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(request, cancellationToken);
        var decoded = Decode(bytes);
        var delimiter = DetectDelimiter(decoded.Text);
        var warnings = new List<string>();
        var previewRows = new List<IReadOnlyDictionary<string, string>>();
        var valid = 0;
        var rejected = 0;

        using var reader = new StringReader(decoded.Text);
        using var csv = new CsvReader(reader, CreateConfiguration(delimiter));
        if (!await csv.ReadAsync() || !csv.ReadHeader())
            throw new InvalidDataException("El CSV no contiene una fila de encabezados válida.");

        var headers = csv.HeaderRecord ?? [];
        ValidateHeaders(headers);
        var rowNumber = 1;
        while (previewRows.Count < 10 && await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var row = ReadRow(csv, headers);
            previewRows.Add(row);
            if (TryBuildEvent(row, Guid.Empty, "preview", rowNumber, out _, out var error)) valid++;
            else
            {
                rejected++;
                warnings.Add($"Fila {rowNumber}: {error}");
            }
        }

        return new CsvPreviewDto(
            Path.GetFileName(request.FileName), decoded.Name, delimiter == "\t" ? "TAB" : delimiter,
            DetectExportType(headers, decoded.Text), headers, previewRows, previewRows.Count,
            valid, rejected, warnings.Take(10).ToList());
    }

    public async Task<ImportJobDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(request, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var duplicate = await dbContext.ImportJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ContentHash == hash &&
                (x.Status == ImportStatus.Completed || x.Status == ImportStatus.CompletedWithWarnings), cancellationToken);
        if (duplicate is not null)
        {
            var duplicateAttempt = new ImportJob
            {
                FileName = Path.GetFileName(request.FileName),
                ContentHash = hash,
                Status = ImportStatus.Duplicate,
                RowsProcessed = duplicate.RowsProcessed,
                RowsDuplicate = duplicate.RowsInserted,
                Warnings = $"El contenido ya fue importado en el trabajo {duplicate.Id}.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.ImportJobs.Add(duplicateAttempt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(duplicateAttempt);
        }

        var job = new ImportJob { FileName = Path.GetFileName(request.FileName), ContentHash = hash };
        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var decoded = Decode(bytes);
            var delimiter = DetectDelimiter(decoded.Text);
            var eventSource = new EventSource { Name = $"CSV:{hash[..12]}", Kind = "CSV" };
            dbContext.EventSources.Add(eventSource);
            await dbContext.SaveChangesAsync(cancellationToken);

            var warnings = new List<string>();
            var batch = new List<AuditEvent>(BatchSize);
            var observedKeys = new HashSet<string>(StringComparer.Ordinal);
            var evidenceOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var evidenceInfo = new Dictionary<Guid, (string Key, int Occurrence)>();
            using var reader = new StringReader(decoded.Text);
            using var csv = new CsvReader(reader, CreateConfiguration(delimiter));
            if (!await csv.ReadAsync() || !csv.ReadHeader())
                throw new InvalidDataException("El CSV no contiene una fila de encabezados válida.");

            var headers = csv.HeaderRecord ?? [];
            ValidateHeaders(headers);
            var exportType = DetectExportType(headers, decoded.Text);
            var rowNumber = 1;
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;
                job.RowsProcessed++;
                var row = ReadRow(csv, headers);
                if (!TryBuildEvent(row, eventSource.Id, hash, rowNumber, out var auditEvent, out var error))
                {
                    job.RowsRejected++;
                    AddWarning(warnings, rowNumber, error);
                    continue;
                }

                var evidenceKey = auditEvent!.DeduplicationKey;
                var occurrence = evidenceOccurrences.GetValueOrDefault(evidenceKey) + 1;
                evidenceOccurrences[evidenceKey] = occurrence;
                auditEvent.DeduplicationKey = Sha256($"{evidenceKey}:{occurrence.ToString(CultureInfo.InvariantCulture)}");
                evidenceInfo[auditEvent.Id] = (evidenceKey, occurrence);

                if (!observedKeys.Add(auditEvent.DeduplicationKey))
                {
                    job.RowsDuplicate++;
                    continue;
                }

                batch.Add(auditEvent);
                if (batch.Count >= BatchSize)
                    await FlushBatchAsync(batch, job, evidenceInfo, cancellationToken);
            }

            await FlushBatchAsync(batch, job, evidenceInfo, cancellationToken);
            job.Status = job.RowsRejected > 0 || warnings.Count > 0
                ? ImportStatus.CompletedWithWarnings
                : ImportStatus.Completed;
            var importSummary = $"Formato: {exportType}; codificación: {decoded.Name}; delimitador: {(delimiter == "\t" ? "TAB" : delimiter)}.";
            job.Warnings = warnings.Count == 0 ? importSummary : importSummary + Environment.NewLine + string.Join(Environment.NewLine, warnings);
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(job);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            var failedJob = await dbContext.ImportJobs.SingleAsync(x => x.Id == job.Id, CancellationToken.None);
            failedJob.Status = ImportStatus.Failed;
            failedJob.Warnings = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            failedJob.CompletedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ImportJobDto>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ImportJobs.AsNoTracking().OrderByDescending(x => x.StartedAtUtc).Take(50)
            .Select(x => new ImportJobDto(x.Id, x.FileName, x.Status, x.RowsProcessed, x.RowsInserted,
                x.RowsDuplicate, x.RowsRejected, x.Warnings, x.StartedAtUtc, x.CompletedAtUtc))
            .ToListAsync(cancellationToken);

    private async Task FlushBatchAsync(
        List<AuditEvent> batch,
        ImportJob job,
        Dictionary<Guid, (string Key, int Occurrence)> evidenceInfo,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;
        var keys = batch.Select(x => x.DeduplicationKey).ToList();
        var existing = await dbContext.AuditEvents.AsNoTracking()
            .Where(x => keys.Contains(x.DeduplicationKey))
            .Select(x => x.DeduplicationKey)
            .ToHashSetAsync(cancellationToken);
        var rawMessages = batch.Select(x => x.RawMessage).Distinct().ToList();
        var timestamps = batch.Select(x => x.EventTimestampUtc).Distinct().ToList();
        var legacyRows = await dbContext.AuditEvents.AsNoTracking()
            .Where(x => x.ParserVersion != BasicSambaMessageParser.Version &&
                rawMessages.Contains(x.RawMessage) && timestamps.Contains(x.EventTimestampUtc))
            .Select(x => new AuditEvent
            {
                Id = x.Id,
                EventTimestampUtc = x.EventTimestampUtc,
                SmbUser = x.SmbUser,
                ClientIp = x.ClientIp,
                Operation = x.Operation,
                OperationResult = x.OperationResult,
                FilePath = x.FilePath,
                SourcePath = x.SourcePath,
                DestinationPath = x.DestinationPath,
                RawMessage = x.RawMessage,
                DeduplicationKey = x.DeduplicationKey,
                ParserVersion = x.ParserVersion
            })
            .ToListAsync(cancellationToken);
        var legacyCounts = legacyRows.GroupBy(EvidenceKey).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var additions = batch.Where(auditEvent =>
        {
            var info = evidenceInfo[auditEvent.Id];
            return !existing.Contains(auditEvent.DeduplicationKey) &&
                (!legacyCounts.TryGetValue(info.Key, out var legacyCount) || info.Occurrence > legacyCount);
        }).ToList();
        job.RowsDuplicate += batch.Count - additions.Count;
        job.RowsInserted += additions.Count;
        dbContext.AuditEvents.AddRange(additions);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var auditEvent in additions) dbContext.Entry(auditEvent).State = EntityState.Detached;
        foreach (var auditEvent in batch) evidenceInfo.Remove(auditEvent.Id);
        batch.Clear();
    }

    private bool TryBuildEvent(
        IReadOnlyDictionary<string, string> row,
        Guid eventSourceId,
        string fileHash,
        int rowNumber,
        out AuditEvent? auditEvent,
        out string? error)
    {
        auditEvent = null;
        error = null;
        var raw = Find(row, "mensaje", "message", "rawmessage", "detalle", "descripcion", "contenido", "log", "event") ?? string.Empty;
        var timestampText = Find(row, "fechahora", "timestamp", "datetime", "eventtime", "fecha", "date", "tiempo");
        var timeText = Find(row, "hora", "time");
        if (!string.IsNullOrWhiteSpace(timestampText) && !string.IsNullOrWhiteSpace(timeText) && !timestampText.Contains(':'))
            timestampText = $"{timestampText} {timeText}";
        if (!TryParseTimestamp(timestampText, out var timestamp))
        {
            error = "fecha ausente, inválida o ambigua; use una fecha con hora y zona explícita cuando corresponda";
            return false;
        }

        ParsedAuditEvent? parsed = null;
        _ = parser.TryParse(raw, out parsed);
        var operation = parsed?.Operation ?? Find(row, "operacion", "operation", "accion", "action", "evento")?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(operation))
        {
            error = "operación no reconocida";
            return false;
        }

        var path = parsed?.FilePath ?? Find(row, "ruta", "path", "archivo", "file", "nombrearchivo", "filepath");
        var sourcePath = parsed?.SourcePath ?? Find(row, "rutaorigen", "sourcepath", "origenruta");
        var destinationPath = parsed?.DestinationPath ?? Find(row, "rutadestino", "destinationpath", "destinoruta");
        var action = parsed?.Action ?? Classify(operation, sourcePath, destinationPath);
        var result = parsed?.Result ?? Find(row, "resultado", "result", "estado", "status") ?? "unknown";
        var user = parsed?.SmbUser ?? Find(row, "usuario", "user", "cuenta", "smbuser", "account");
        var clientIp = parsed?.ClientIp ?? Find(row, "ip", "clientip", "direccionip", "remotehost");
        var normalizedRaw = NormalizeEvidence(string.IsNullOrWhiteSpace(raw) ? JsonSerializer.Serialize(row) : raw);
        auditEvent = new AuditEvent
        {
            EventTimestampUtc = timestamp.ToUniversalTime(),
            SmbUser = user,
            ClientIp = clientIp,
            Operation = operation,
            InterpretedAction = action,
            InterpretationConfidence = parsed?.Confidence ?? (action == AuditAction.Unknown ? InterpretationConfidence.Unknown : InterpretationConfidence.Medium),
            OperationResult = result,
            ShareName = parsed?.ShareName ?? Find(row, "recurso", "share", "carpetacompartida") ?? ExtractShare(destinationPath ?? sourcePath ?? path),
            FilePath = path ?? destinationPath,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            FileExtension = ExtensionOf(destinationPath ?? path ?? sourcePath),
            ObjectType = parsed?.ObjectType ?? Find(row, "tipoobjeto", "objecttype", "type"),
            IsAuxiliaryStream = parsed?.IsAuxiliaryStream ?? IsAuxiliaryStream(destinationPath ?? path ?? sourcePath),
            IsRecyclePath = parsed?.IsRecyclePath ?? (IsRecyclePath(sourcePath) || IsRecyclePath(destinationPath) || IsRecyclePath(path)),
            RawMessage = string.IsNullOrWhiteSpace(raw) ? JsonSerializer.Serialize(row) : raw,
            EventSourceId = eventSourceId,
            DeduplicationKey = string.Empty,
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Historical,
            MetadataJson = JsonSerializer.Serialize(new { importRow = rowNumber, importFileHash = fileHash })
        };
        auditEvent.DeduplicationKey = EvidenceKey(auditEvent);
        return true;
    }

    private static async Task<byte[]> ReadBytesAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        if (!Path.GetExtension(request.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Solo se permiten archivos CSV.");
        if (request.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("El CSV debe contener datos y no superar 20 MB.");
        await using var buffer = new MemoryStream((int)request.Length);
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaxBytes) throw new InvalidDataException("El CSV supera el límite de 20 MB.");
        return buffer.ToArray();
    }

    private static CsvConfiguration CreateConfiguration(string delimiter) => new(CultureInfo.InvariantCulture)
    {
        Delimiter = delimiter,
        BadDataFound = null,
        MissingFieldFound = null,
        HeaderValidated = null,
        TrimOptions = TrimOptions.Trim,
        IgnoreBlankLines = true
    };

    private static Dictionary<string, string> ReadRow(CsvReader csv, string[] headers)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Length; index++)
        {
            var key = NormalizeHeader(headers[index]);
            if (key.Length == 0) key = $"columna{index + 1}";
            row[key] = csv.GetField(index)?.Trim() ?? string.Empty;
        }
        return row;
    }

    private static void ValidateHeaders(string[] headers)
    {
        if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("El CSV no contiene encabezados reconocibles.");
        if (headers.Length > 200)
            throw new InvalidDataException("El CSV contiene demasiadas columnas para una exportación de auditoría.");
    }

    private static (string Text, string Name) Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            return (Encoding.UTF8.GetString(bytes, Encoding.UTF8.Preamble.Length, bytes.Length - Encoding.UTF8.Preamble.Length), "UTF-8 BOM");
        try { return (new UTF8Encoding(false, true).GetString(bytes), "UTF-8"); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return (Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes), "Windows-1252");
        }
    }

    private static string DetectDelimiter(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Take(5).ToList();
        if (lines.Count == 0) throw new InvalidDataException("El CSV está vacío.");
        var candidates = new[] { ",", ";", "\t" };
        var winner = candidates.Select(candidate => new
        {
            Candidate = candidate,
            Counts = lines.Select(line => CountOutsideQuotes(line, candidate[0])).ToArray()
        })
            .OrderByDescending(x => x.Counts[0] > 0)
            .ThenBy(x => x.Counts.Max() - x.Counts.Min())
            .ThenByDescending(x => x.Counts.Sum())
            .First();
        if (winner.Counts[0] == 0) throw new InvalidDataException("No se pudo detectar un delimitador CSV compatible (coma, punto y coma o tabulación).");
        return winner.Candidate;
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') index++;
                else quoted = !quoted;
            }
            else if (!quoted && line[index] == delimiter) count++;
        }
        return count;
    }

    private static string DetectExportType(string[] headers, string text)
    {
        var normalized = headers.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (text.Contains("ugreen_syslog|", StringComparison.OrdinalIgnoreCase)) return "UGOS Pro · auditoría SMB";
        if (normalized.Any(x => x.Contains("transmision", StringComparison.OrdinalIgnoreCase) || x.Contains("transfer", StringComparison.OrdinalIgnoreCase))) return "UGOS Pro · registros de transmisión";
        if (normalized.Any(x => x is "evento" or "suceso" or "event" or "message" or "mensaje")) return "UGOS Pro · registros de sucesos";
        return "CSV histórico compatible";
    }

    private static string NormalizeHeader(string header)
    {
        var decomposed = header.Trim().TrimStart('\ufeff').ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)).ToArray());
    }

    private static string? Find(IReadOnlyDictionary<string, string> row, params string[] names)
    {
        foreach (var name in names)
            if (row.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        return null;
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (Regex.IsMatch(value, "(?:Z|[+-]\\d{2}:?\\d{2})$", RegexOptions.IgnoreCase) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out timestamp)) return true;

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "d-M-yyyy H:mm:ss", "d/M/yyyy H:mm:ss",
            "dd-MM-yyyy HH:mm", "dd/MM/yyyy HH:mm", "d-M-yyyy H:mm", "d/M/yyyy H:mm"
        };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var local)) return false;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zone = FindSantiagoTimeZone();
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return false;
        timestamp = new DateTimeOffset(local, zone.GetUtcOffset(local));
        return true;
    }

    private static TimeZoneInfo FindSantiagoTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Santiago"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time"); }
    }

    private static AuditAction Classify(string operation, string? source, string? destination) => operation.ToLowerInvariant() switch
    {
        "create" or "creation" or "mkdir" or "mkdirat" => AuditAction.Creation,
        "open" => AuditAction.Open,
        "pread" or "read" => AuditAction.ObservedRead,
        "pwrite" or "write" or "ftruncate" or "truncate" or "modification" => AuditAction.Write,
        "unlinkat" or "unlink" or "rmdir" or "delete" or "deletion" => AuditAction.Deletion,
        "renameat" or "rename" when ParentPath(source) != ParentPath(destination) => AuditAction.Move,
        "renameat" or "rename" => AuditAction.Rename,
        "connect" or "disconnect" or "connection" => AuditAction.Connection,
        _ => AuditAction.Unknown
    };

    private static string? ParentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? null : normalized[..slash].ToLowerInvariant();
    }

    private static string? ExtractShare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/');
        const string prefix = "/volume1/";
        var index = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var remainder = normalized[(index + prefix.Length)..];
        var slash = remainder.IndexOf('/');
        return slash < 0 ? remainder : remainder[..slash];
    }

    private static bool IsAuxiliaryStream(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.IndexOf(':', normalized.LastIndexOf('/') + 1) >= 0;
    }

    private static bool IsRecyclePath(string? path) => path?.Replace('\\', '/').Contains("#recycle", StringComparison.OrdinalIgnoreCase) == true;

    private static string? ExtensionOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Split(':')[0];
        var extension = Path.GetExtension(normalized);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.ToLowerInvariant();
    }

    private static string NormalizeEvidence(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static string EvidenceKey(AuditEvent auditEvent) => Sha256(string.Join('|',
        "csv-v2", auditEvent.EventTimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        NormalizeEvidence(auditEvent.SmbUser), NormalizeEvidence(auditEvent.ClientIp), auditEvent.Operation,
        NormalizeEvidence(auditEvent.OperationResult), NormalizeEvidence(auditEvent.FilePath),
        NormalizeEvidence(auditEvent.SourcePath), NormalizeEvidence(auditEvent.DestinationPath),
        NormalizeEvidence(auditEvent.RawMessage)));

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void AddWarning(List<string> warnings, int rowNumber, string? error)
    {
        if (warnings.Count < 50) warnings.Add($"Fila {rowNumber}: {error}");
        else if (warnings.Count == 50) warnings.Add("Se omitieron advertencias adicionales; consulte los contadores del trabajo.");
    }

    private static ImportJobDto Map(ImportJob job) => new(
        job.Id, job.FileName, job.Status, job.RowsProcessed, job.RowsInserted, job.RowsDuplicate,
        job.RowsRejected, job.Warnings, job.StartedAtUtc, job.CompletedAtUtc);
}
