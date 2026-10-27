using System.Text;
using BCMetals.NasAudit.Application.Insights;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Insights;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class InsightsServiceTests
{
    [Fact]
    public async Task ExplorerIncludesRenameSourceAndDestinationInTimeline()
    {
        await using var context = CreateContext();
        AddEvent(context, "renameat", AuditAction.Rename, "/volume1/PRUEBA_AUDITORIA_SMB/new.txt",
            sourcePath: "/volume1/PRUEBA_AUDITORIA_SMB/old.txt", destinationPath: "/volume1/PRUEBA_AUDITORIA_SMB/new.txt");
        await context.SaveChangesAsync();
        var service = new InsightsService(context);

        var paths = await service.GetPathsAsync(new PathQuery(DataMode.Live), CancellationToken.None);
        var timeline = await service.GetPathTimelineAsync(
            "/volume1/PRUEBA_AUDITORIA_SMB/old.txt", DataMode.Live, CancellationToken.None);

        Assert.Equal(2, paths.TotalItems);
        Assert.NotNull(timeline);
        Assert.Single(timeline.Events);
        Assert.Equal("renameat", timeline.LastKnownOperation);
        Assert.True(timeline.RenamedOrMovedAway);
    }

    [Fact]
    public async Task UserProfileAndActivityGroupsAreDerivedFromEvidence()
    {
        await using var context = CreateContext();
        AddEvent(context, "create_file", AuditAction.Creation, "/volume1/PRUEBA_AUDITORIA_SMB/test.txt");
        AddEvent(context, "pwrite", AuditAction.Write, "/volume1/PRUEBA_AUDITORIA_SMB/test.txt", seconds: 30);
        await context.SaveChangesAsync();
        var service = new InsightsService(context);
        var range = new InsightRange(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), DataMode.Live);

        var profile = await service.GetUserActivityAsync("soporte", range, CancellationToken.None);
        var groups = await service.GetActivityGroupsAsync(range, 10, CancellationToken.None);
        var folder = await service.GetFolderHistoryAsync("/volume1", range, CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(2, profile.Summary.EventCount);
        Assert.Single(profile.ActivityByDay);
        Assert.Single(groups);
        Assert.Equal(2, groups[0].EventCount);
        Assert.Contains("pwrite", groups[0].Operations);
        Assert.NotNull(folder);
        Assert.Contains(folder.Subfolders, item => item.Path == "/volume1/PRUEBA_AUDITORIA_SMB");
    }

    [Fact]
    public async Task DownloadableUserReportNeutralizesSpreadsheetFormula()
    {
        await using var context = CreateContext();
        AddEvent(context, "connect", AuditAction.Connection, "/volume1/PRUEBA_AUDITORIA_SMB", user: "=FORMULA");
        await context.SaveChangesAsync();
        var service = new InsightsService(context);
        var range = new InsightRange(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), DataMode.Live);

        var report = await service.ExportAsync(ReportKind.Users, range, CancellationToken.None);

        Assert.Equal(1, report.RowCount);
        Assert.Contains("'=FORMULA", Encoding.UTF8.GetString(report.Content), StringComparison.Ordinal);
    }

    private static void AddEvent(
        AuditDbContext context,
        string operation,
        AuditAction action,
        string filePath,
        string? sourcePath = null,
        string? destinationPath = null,
        int seconds = 0,
        string user = "soporte")
    {
        var source = context.EventSources.Local.FirstOrDefault() ?? new EventSource { Name = "Journal:test", Kind = "Journal" };
        if (source.Id == Guid.Empty || context.Entry(source).State == EntityState.Detached) context.EventSources.Add(source);
        context.AuditEvents.Add(new AuditEvent
        {
            EventTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-5).AddSeconds(seconds),
            SmbUser = user,
            ClientIp = "client.example.invalid",
            Operation = operation,
            InterpretedAction = action,
            InterpretationConfidence = InterpretationConfidence.Confirmed,
            OperationResult = "ok",
            ShareName = "PRUEBA_AUDITORIA_SMB",
            FilePath = filePath,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            RawMessage = $"ugreen_syslog|{operation}",
            EventSource = source,
            EventSourceId = source.Id,
            DeduplicationKey = Guid.NewGuid().ToString("N"),
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Live
        });
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AuditDbContext(options);
    }
}
