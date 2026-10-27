using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Importing;

public sealed record ImportJobDto(
    Guid Id,
    string FileName,
    ImportStatus Status,
    int RowsProcessed,
    int RowsInserted,
    int RowsDuplicate,
    int RowsRejected,
    string? Warnings,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ImportRequest(Stream Content, string FileName, long Length);

public sealed record CsvPreviewDto(
    string FileName,
    string Encoding,
    string Delimiter,
    string ExportType,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    int SampledRows,
    int ValidRows,
    int RejectedRows,
    IReadOnlyList<string> Warnings);

