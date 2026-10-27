using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset EventTimestampUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? SmbUser { get; set; }
    public string? ClientIp { get; set; }
    public required string Operation { get; set; }
    public AuditAction InterpretedAction { get; set; }
    public InterpretationConfidence InterpretationConfidence { get; set; }
    public required string OperationResult { get; set; }
    public string? ShareName { get; set; }
    public string? FilePath { get; set; }
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }
    public string? FileExtension { get; set; }
    public string? ObjectType { get; set; }
    public bool IsAuxiliaryStream { get; set; }
    public bool IsRecyclePath { get; set; }
    public required string RawMessage { get; set; }
    public Guid EventSourceId { get; set; }
    public EventSource? EventSource { get; set; }
    public string? JournalCursor { get; set; }
    public string? BootId { get; set; }
    public required string DeduplicationKey { get; set; }
    public required string ParserVersion { get; set; }
    public DataMode DataMode { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

