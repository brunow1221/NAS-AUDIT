using System.Collections.Concurrent;
using System.Threading.Channels;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Events;

namespace BCMetals.NasAudit.Infrastructure.Ingestion;

public sealed class LiveEventPublisher : ILiveEventPublisher
{
    private readonly ConcurrentDictionary<Guid, Channel<AuditEventDto>> _subscribers = new();

    public ValueTask PublishAsync(AuditEventDto auditEvent, CancellationToken cancellationToken)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(auditEvent);
        return ValueTask.CompletedTask;
    }

    public ILiveEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<AuditEventDto>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers[id] = channel;
        return new Subscription(id, channel.Reader, RemoveSubscriber);
    }

    private void RemoveSubscriber(Guid id) => _subscribers.TryRemove(id, out _);

    private sealed class Subscription(
        Guid id,
        ChannelReader<AuditEventDto> reader,
        Action<Guid> unsubscribe) : ILiveEventSubscription
    {
        private int _disposed;

        public IAsyncEnumerable<AuditEventDto> ReadAllAsync(CancellationToken cancellationToken) =>
            reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) unsubscribe(id);
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class NullLiveEventPublisher : ILiveEventPublisher
{
    public ValueTask PublishAsync(AuditEventDto auditEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ILiveEventSubscription Subscribe() => NullSubscription.Instance;

    private sealed class NullSubscription : ILiveEventSubscription
    {
        public static readonly NullSubscription Instance = new();

        public IAsyncEnumerable<AuditEventDto> ReadAllAsync(CancellationToken cancellationToken) =>
            Empty(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<AuditEventDto> Empty(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
