namespace BCMetals.NasAudit.Domain.Entities;

public sealed class IngestionBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CollectorId { get; set; }
    public Guid BatchId { get; set; }
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
    public int Rejected { get; set; }
    public string? ConfirmedCursor { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
