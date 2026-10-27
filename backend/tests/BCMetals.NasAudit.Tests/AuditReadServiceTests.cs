using System.Globalization;
using System.Text;
using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using BCMetals.NasAudit.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class AuditReadServiceTests
{
    [Fact]
    public async Task CsvExportNeutralizesSpreadsheetFormulas()
    {
        await using var context = CreateContext();
        var source = new EventSource { Name = "CSV:test", Kind = "CSV" };
        context.EventSources.Add(source);
        context.AuditEvents.Add(new AuditEvent
        {
            EventTimestampUtc = DateTimeOffset.Parse("2026-10-20T12:00:00Z", CultureInfo.InvariantCulture),
            SmbUser = "=HYPERLINK(\"https://invalid\")",
            Operation = "pwrite",
            InterpretedAction = AuditAction.Write,
            InterpretationConfidence = InterpretationConfidence.Confirmed,
            OperationResult = "ok",
            RawMessage = "+cmd",
            EventSource = source,
            EventSourceId = source.Id,
            DeduplicationKey = Guid.NewGuid().ToString("N"),
            ParserVersion = BasicSambaMessageParser.Version,
            DataMode = DataMode.Historical
        });
        await context.SaveChangesAsync();

        var service = new AuditReadService(context);
        var export = await service.ExportEventsAsync(Query(), CancellationToken.None);
        var csv = Encoding.UTF8.GetString(export.Content);

        Assert.Contains("'=HYPERLINK", csv, StringComparison.Ordinal);
        Assert.Contains("'+cmd", csv, StringComparison.Ordinal);
    }

    private static EventQuery Query() => new(
        FromUtc: null, ToUtc: null, User: null, ClientIp: null, Operation: null, Action: null,
        Share: null, FileName: null, Path: null, Extension: null, Result: null, Source: null,
        Confidence: null, Search: null);

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AuditDbContext(options);
    }
}
