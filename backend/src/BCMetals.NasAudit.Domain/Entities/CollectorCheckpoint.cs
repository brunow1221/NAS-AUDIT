namespace BCMetals.NasAudit.Domain.Entities;

public sealed class CollectorCheckpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CollectorId { get; set; }
    public Guid EventSourceId { get; set; }
    public EventSource? EventSource { get; set; }
    public string? Cursor { get; set; }
    public string? BootId { get; set; }
    public DateTimeOffset? LastEventUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
