namespace BCMetals.NasAudit.Domain.Entities;

public sealed class EventSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AuditEvent> Events { get; set; } = [];
}

