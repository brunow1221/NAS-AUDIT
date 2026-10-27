using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Ingestion;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class LiveEventPublisherTests
{
    [Fact]
    public async Task SubscriptionQueuesEventsPublishedBeforeEnumerationStarts()
    {
        var publisher = new LiveEventPublisher();
        await using var subscription = publisher.Subscribe();
        var expected = Event();

        await publisher.PublishAsync(expected, CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var reader = subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(expected.Id, reader.Current.Id);
    }

    private static AuditEventDto Event() => new(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "soporte", "client.example.invalid", "pwrite",
        AuditAction.Write, InterpretationConfidence.Confirmed, "ok", "PRUEBA_AUDITORIA_SMB",
        "/volume1/PRUEBA_AUDITORIA_SMB/test.txt", null, null, ".txt", "file", false, false,
        "ugreen_syslog|test", "Journal:test", "Journal", "cursor-1", "boot-1", "{}",
        DataMode.Live, "phase2-ugos-v2");
}
