namespace BCMetals.NasAudit.Domain.Entities;

public sealed class MaintenanceRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Kind { get; set; }
    public required string Status { get; set; }
    public required string Actor { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long AffectedRows { get; set; }
    public string DetailsJson { get; set; } = "{}";
}
