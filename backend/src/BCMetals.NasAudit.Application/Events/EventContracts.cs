using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Events;

public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset EventTimestampUtc,
    string? SmbUser,
    string? ClientIp,
    string Operation,
    AuditAction InterpretedAction,
    InterpretationConfidence InterpretationConfidence,
    string OperationResult,
    string? ShareName,
    string? FilePath,
    string? SourcePath,
    string? DestinationPath,
    string? FileExtension,
    string? ObjectType,
    bool IsAuxiliaryStream,
    bool IsRecyclePath,
    string RawMessage,
    string SourceName,
    string SourceKind,
    string? JournalCursor,
    string? BootId,
    string MetadataJson,
    DataMode DataMode,
    string ParserVersion);

public sealed record EventQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? User,
    string? ClientIp,
    string? Operation,
    AuditAction? Action,
    string? Share,
    string? FileName,
    string? Path,
    string? Extension,
    string? Result,
    string? Source,
    InterpretationConfidence? Confidence,
    string? Search,
    DataMode DataMode = DataMode.Historical,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "timestamp",
    bool Descending = true);

public sealed record EventExport(byte[] Content, string FileName, long RowCount);

