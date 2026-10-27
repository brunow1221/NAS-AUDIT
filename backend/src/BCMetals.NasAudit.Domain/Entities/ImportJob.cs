using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Domain.Entities;

public sealed class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public required string ContentHash { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Processing;
    public int RowsProcessed { get; set; }
    public int RowsInserted { get; set; }
    public int RowsDuplicate { get; set; }
    public int RowsRejected { get; set; }
    public string? Warnings { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

