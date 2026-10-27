using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Operations;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class OperationsTests
{
    [Fact]
    public async Task AlertCandidatesUseExplicitThresholdAndCautionaryLanguage()
    {
        await using var context = CreateContext();
        context.AuditEvents.Add(Event(AuditAction.Deletion));
        await context.SaveChangesAsync();
        var evaluator = new AlertCandidateEvaluator(context, Options.Create(new MonitoringOptions
        {
            BehavioralCandidatesEnabled = true,
            DeletionCandidateThreshold = 1,
            WriteCandidateThreshold = 500
        }));

        var result = await evaluator.EvaluateAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("deletion-volume", candidate.RuleKey);
        Assert.Contains("no constituye", candidate.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetentionPreviewCountsRowsWithoutDeletingWhenDisabled()
    {
        await using var context = CreateContext();
        var old = Event(AuditAction.Open);
        old.EventTimestampUtc = DateTimeOffset.UtcNow.AddDays(-100);
        context.AuditEvents.Add(old);
        await context.SaveChangesAsync();
        var service = new RetentionService(context, Options.Create(new RetentionOptions
        {
            Enabled = false,
            HistoricalDays = 30,
            LiveDays = 30,
            DemoDays = 30
        }));

        var preview = await service.GetPreviewAsync(CancellationToken.None);

        Assert.False(preview.Policy.Enabled);
        Assert.Equal(1, preview.Events.Single(x => x.DataMode == DataMode.Live).EligibleRows);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync("test", CancellationToken.None));
        Assert.Single(context.AuditEvents);
    }

    [Fact]
    public void RetentionSchedulerAlwaysTargetsAFutureRun()
    {
        var delay = RetentionBackgroundService.DelayUntilNextRun(DateTimeOffset.UtcNow.Hour);
        Assert.True(delay > TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromDays(1));
    }

    private static AuditEvent Event(AuditAction action)
    {
        var source = new EventSource { Name = $"test-{Guid.NewGuid():N}", Kind = "Test" };
        return new AuditEvent
        {
            EventTimestampUtc = DateTimeOffset.UtcNow,
            Operation = action == AuditAction.Deletion ? "unlinkat" : "create_file",
            InterpretedAction = action,
            InterpretationConfidence = InterpretationConfidence.Confirmed,
            OperationResult = "ok",
            FilePath = "/volume1/PRUEBA_AUDITORIA_SMB/test.txt",
            RawMessage = "synthetic test",
            EventSource = source,
            EventSourceId = source.Id,
            DeduplicationKey = Guid.NewGuid().ToString("N"),
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Live
        };
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AuditDbContext(options);
    }
}
