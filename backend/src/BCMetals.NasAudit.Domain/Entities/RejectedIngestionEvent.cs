namespace BCMetals.NasAudit.Domain.Entities;

public sealed class RejectedIngestionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CollectorId { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public string Cursor { get; set; } = string.Empty;
    public string? BootId { get; set; }
    public DateTimeOffset EventTimestampUtc { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
