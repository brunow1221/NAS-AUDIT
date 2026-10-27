namespace BCMetals.NasAudit.Domain.Entities;

public sealed class CollectorHeartbeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CollectorId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastEventUtc { get; set; }
    public int QueueDepth { get; set; }
    public string? Message { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
