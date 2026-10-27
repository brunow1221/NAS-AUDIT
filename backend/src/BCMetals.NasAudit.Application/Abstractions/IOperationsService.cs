using BCMetals.NasAudit.Application.Operations;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface IOperationsService
{
    Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken);
}

public interface IRetentionService
{
    Task<RetentionPreviewDto> GetPreviewAsync(CancellationToken cancellationToken);
    Task<RetentionRunDto> RunAsync(string actor, CancellationToken cancellationToken);
}

public interface IOperationalEventBuffer
{
    void Record(string severity, string code, string message);
    IReadOnlyList<OperationalEventDto> Snapshot(int limit = 20);
}
