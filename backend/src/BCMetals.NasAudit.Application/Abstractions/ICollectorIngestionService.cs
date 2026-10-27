using BCMetals.NasAudit.Application.Ingestion;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface ICollectorIngestionService
{
    Task<IngestionBatchResult> IngestAsync(IngestionBatchRequest request, CancellationToken cancellationToken);
    Task RecordHeartbeatAsync(CollectorHeartbeatRequest request, CancellationToken cancellationToken);
    Task<CollectorStatusDto> GetStatusAsync(CancellationToken cancellationToken);
}
