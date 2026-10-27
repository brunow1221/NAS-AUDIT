using BCMetals.NasAudit.Application.Events;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface ILiveEventPublisher
{
    ValueTask PublishAsync(AuditEventDto auditEvent, CancellationToken cancellationToken);
    ILiveEventSubscription Subscribe();
}

public interface ILiveEventSubscription : IAsyncDisposable
{
    IAsyncEnumerable<AuditEventDto> ReadAllAsync(CancellationToken cancellationToken);
}
