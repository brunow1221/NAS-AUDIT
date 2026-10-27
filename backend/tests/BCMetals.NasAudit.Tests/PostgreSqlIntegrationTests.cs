using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Application.Insights;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Insights;
using BCMetals.NasAudit.Infrastructure.Operations;
using BCMetals.NasAudit.Infrastructure.Persistence;
using BCMetals.NasAudit.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class PostgreSqlIntegrationTests
{
    [Fact]
    public async Task MigrationAndFilteredHistoryWorkAgainstPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new AuditDbContext(options);
        await context.Database.MigrateAsync();

        var marker = Guid.NewGuid().ToString("N");
        var source = new EventSource { Name = $"integration:{marker}", Kind = "CSV" };
        context.EventSources.Add(source);
        context.AuditEvents.Add(new AuditEvent
        {
            EventTimestampUtc = DateTimeOffset.UtcNow,
            SmbUser = $"integration-{marker}",
            ClientIp = "client.example.invalid",
            Operation = "pwrite",
            InterpretedAction = AuditAction.Write,
            InterpretationConfidence = InterpretationConfidence.Confirmed,
            OperationResult = "ok",
            FilePath = $"/volume1/PRUEBA_AUDITORIA_SMB/{marker}.txt",
            RawMessage = $"integration {marker}",
            EventSource = source,
            EventSourceId = source.Id,
            DeduplicationKey = marker,
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Historical
        });
        await context.SaveChangesAsync();

        var result = await new AuditReadService(context).GetEventsAsync(new EventQuery(
            FromUtc: null, ToUtc: null, User: null, ClientIp: null, Operation: null, Action: null,
            Share: null, FileName: null, Path: null, Extension: null, Result: null, Source: null,
            Confidence: null, Search: marker), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal($"integration-{marker}", result.Items[0].SmbUser);

        var insights = new InsightsService(context);
        var paths = await insights.GetPathsAsync(new PathQuery(DataMode.Historical, Search: marker), CancellationToken.None);
        var range = new InsightRange(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), DataMode.Historical);
        var users = await insights.GetUsersAsync(new PathQuery(DataMode.Historical, Search: marker,
            FromUtc: range.FromUtc, ToUtc: range.ToUtc), CancellationToken.None);
        var user = await insights.GetUserActivityAsync($"integration-{marker}", range, CancellationToken.None);
        var folder = await insights.GetFolderHistoryAsync("/volume1", range, CancellationToken.None);
        var statistics = await insights.GetStatisticsAsync(range, CancellationToken.None);
        var userReport = await insights.ExportAsync(ReportKind.Users, range, CancellationToken.None);
        var retention = new RetentionService(context, Options.Create(new RetentionOptions { Enabled = false }));
        var retentionPreview = await retention.GetPreviewAsync(CancellationToken.None);
        var alerts = await new AlertCandidateEvaluator(context, Options.Create(new MonitoringOptions
        {
            BehavioralCandidatesEnabled = true,
            WriteCandidateThreshold = 1
        })).EvaluateAsync(range.FromUtc, range.ToUtc, CancellationToken.None);

        Assert.Single(paths.Items);
        Assert.Single(users.Items);
        Assert.NotNull(user);
        Assert.NotNull(folder);
        Assert.Contains(folder.Subfolders, item => item.Path == "/volume1/PRUEBA_AUDITORIA_SMB");
        Assert.True(statistics.TotalEvents > 0);
        Assert.True(userReport.RowCount > 0);
        Assert.NotNull(retentionPreview.Policy);
        Assert.Contains(alerts, item => item.RuleKey == "write-volume");
    }
}
