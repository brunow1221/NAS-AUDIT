using BCMetals.NasAudit.Application.Ingestion;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Ingestion;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class CollectorIngestionServiceTests
{
    [Fact]
    public async Task ConfirmedBatchPersistsLiveEventAndAdvancesCheckpoint()
    {
        await using var context = CreateContext();
        var service = new CollectorIngestionService(context, new BasicSambaMessageParser(), new NullLiveEventPublisher());
        var request = Batch("cursor-1");

        var result = await service.IngestAsync(request, CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal("cursor-1", result.ConfirmedCursor);
        var stored = await context.AuditEvents.SingleAsync();
        Assert.Equal(DataMode.Live, stored.DataMode);
        Assert.Equal("cursor-1", stored.JournalCursor);
        Assert.Equal("cursor-1", (await context.CollectorCheckpoints.SingleAsync()).Cursor);
    }

    [Fact]
    public async Task ReplayedBatchReturnsOriginalResultWithoutDuplicatingEvidence()
    {
        await using var context = CreateContext();
        var service = new CollectorIngestionService(context, new BasicSambaMessageParser(), new NullLiveEventPublisher());
        var request = Batch("cursor-2");
        var first = await service.IngestAsync(request, CancellationToken.None);

        var replay = await service.IngestAsync(request, CancellationToken.None);

        Assert.False(first.Replay);
        Assert.True(replay.Replay);
        Assert.Equal(first.Accepted, replay.Accepted);
        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task UnsupportedMessageIsPreservedAndDoesNotBlockCheckpoint()
    {
        await using var context = CreateContext();
        var service = new CollectorIngestionService(context, new BasicSambaMessageParser(), new NullLiveEventPublisher());
        var request = new IngestionBatchRequest(
            "bcmetalsnas-test", Guid.NewGuid(),
            [new CollectorEventRequest("cursor-rejected", "boot-1", DateTimeOffset.UtcNow.AddMinutes(-1),
                "ugreen_syslog|formato-incompleto", null)]);

        var result = await service.IngestAsync(request, CancellationToken.None);

        Assert.Equal(1, result.Rejected);
        Assert.Equal("cursor-rejected", result.ConfirmedCursor);
        Assert.Equal("ugreen_syslog|formato-incompleto", (await context.RejectedIngestionEvents.SingleAsync()).RawMessage);
        Assert.Equal("cursor-rejected", (await context.CollectorCheckpoints.SingleAsync()).Cursor);
    }

    private static IngestionBatchRequest Batch(string cursor) => new(
        "bcmetalsnas-test", Guid.Parse("d2619a4d-bf73-4c62-8ca7-b1cc3ddb8558"),
        [new CollectorEventRequest(cursor, "boot-1", DateTimeOffset.UtcNow.AddMinutes(-1),
            "ugreen_syslog|soporte|client.example.invalid|pwrite|ok|/volume1/PRUEBA_AUDITORIA_SMB/test.txt", null)]);

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AuditDbContext(options);
    }
}
