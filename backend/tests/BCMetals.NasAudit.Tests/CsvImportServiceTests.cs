using System.Text;
using BCMetals.NasAudit.Application.Importing;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Importing;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class CsvImportServiceTests
{
    [Fact]
    public async Task ImportPersistsCreationAndOpenAsDifferentActions()
    {
        await using var context = CreateContext();
        var service = new CsvImportService(context, new BasicSambaMessageParser());
        var csv = "timestamp,message\n" +
            "2026-10-20T12:00:00Z,ugreen_syslog|demo|client.example.invalid|create_file|ok|0x1|file|create|/volume1/PRUEBA_AUDITORIA_SMB/demo.txt\n" +
            "2026-10-20T12:01:00Z,ugreen_syslog|demo|client.example.invalid|create_file|ok|0x1|file|open|/volume1/PRUEBA_AUDITORIA_SMB/demo.txt\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var result = await service.ImportAsync(
            new ImportRequest(new MemoryStream(bytes), "events.csv", bytes.Length),
            CancellationToken.None);

        Assert.Equal(2, result.RowsInserted);
        var actions = await context.AuditEvents.OrderBy(x => x.EventTimestampUtc).Select(x => x.InterpretedAction).ToListAsync();
        Assert.Equal([AuditAction.Creation, AuditAction.Open], actions);
    }

    [Fact]
    public async Task ReimportingSameContentDoesNotDuplicateEvents()
    {
        await using var context = CreateContext();
        var service = new CsvImportService(context, new BasicSambaMessageParser());
        var bytes = Encoding.UTF8.GetBytes(
            "timestamp,message\n2026-10-20T12:00:00Z,ugreen_syslog|demo|client.example.invalid|unlinkat|ok|/volume1/PRUEBA_AUDITORIA_SMB/demo.txt\n");

        await service.ImportAsync(new ImportRequest(new MemoryStream(bytes), "one.csv", bytes.Length), CancellationToken.None);
        var duplicate = await service.ImportAsync(new ImportRequest(new MemoryStream(bytes), "two.csv", bytes.Length), CancellationToken.None);

        Assert.Equal(ImportStatus.Duplicate, duplicate.Status);
        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task OverlappingExportsUseCanonicalEvidenceForDeduplication()
    {
        await using var context = CreateContext();
        var service = new CsvImportService(context, new BasicSambaMessageParser());
        const string row = "2026-10-20T12:00:00Z,ugreen_syslog|demo|client.example.invalid|unlinkat|ok|/volume1/BCMETALS/demo.txt";
        var first = Encoding.UTF8.GetBytes($"timestamp,message\n{row}\n");
        var second = Encoding.UTF8.GetBytes($"timestamp,message\r\n{row}\r\n");

        await service.ImportAsync(new ImportRequest(new MemoryStream(first), "one.csv", first.Length), CancellationToken.None);
        var overlap = await service.ImportAsync(new ImportRequest(new MemoryStream(second), "two.csv", second.Length), CancellationToken.None);

        Assert.Equal(0, overlap.RowsInserted);
        Assert.Equal(1, overlap.RowsDuplicate);
        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task PreviewDetectsUgosFormatWithoutPersistingEvents()
    {
        await using var context = CreateContext();
        var service = new CsvImportService(context, new BasicSambaMessageParser());
        var bytes = Encoding.UTF8.GetBytes(
            "timestamp;message\n2026-10-20T12:00:00Z;ugreen_syslog|demo|client.example.invalid|pwrite|ok|/volume1/BCMETALS/demo.txt\n");

        var preview = await service.PreviewAsync(
            new ImportRequest(new MemoryStream(bytes), "preview.csv", bytes.Length), CancellationToken.None);

        Assert.Equal(";", preview.Delimiter);
        Assert.Contains("UGOS Pro", preview.ExportType, StringComparison.Ordinal);
        Assert.Equal(1, preview.ValidRows);
        Assert.Empty(context.AuditEvents);
    }

    [Fact]
    public async Task IdenticalRowsInOneExportArePreservedAsSeparateEvidence()
    {
        await using var context = CreateContext();
        var service = new CsvImportService(context, new BasicSambaMessageParser());
        const string row = "2026-10-20T12:00:00Z,ugreen_syslog|demo|client.example.invalid|pwrite|ok|/volume1/BCMETALS/demo.txt";
        var bytes = Encoding.UTF8.GetBytes($"timestamp,message\n{row}\n{row}\n");

        var result = await service.ImportAsync(
            new ImportRequest(new MemoryStream(bytes), "repeated.csv", bytes.Length), CancellationToken.None);

        Assert.Equal(2, result.RowsInserted);
        Assert.Equal(0, result.RowsDuplicate);
        Assert.Equal(2, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task OverlapWithPhaseOneEvidenceIsReconciledWithoutDuplication()
    {
        await using var context = CreateContext();
        const string raw = "ugreen_syslog|demo|client.example.invalid|pwrite|ok|/volume1/BCMETALS/demo.txt";
        var source = new EventSource { Name = "CSV:phase1", Kind = "CSV" };
        context.EventSources.Add(source);
        context.AuditEvents.Add(new AuditEvent
        {
            EventTimestampUtc = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            SmbUser = "demo",
            ClientIp = "client.example.invalid",
            Operation = "pwrite",
            InterpretedAction = AuditAction.Write,
            InterpretationConfidence = InterpretationConfidence.Confirmed,
            OperationResult = "ok",
            FilePath = "/volume1/BCMETALS/demo.txt",
            RawMessage = raw,
            EventSource = source,
            EventSourceId = source.Id,
            DeduplicationKey = "legacy-key",
            ParserVersion = "phase1-basic-v1",
            DataMode = DataMode.Historical
        });
        await context.SaveChangesAsync();
        var bytes = Encoding.UTF8.GetBytes($"timestamp,message\n2026-10-20T12:00:00Z,{raw}\n");

        var result = await new CsvImportService(context, new BasicSambaMessageParser()).ImportAsync(
            new ImportRequest(new MemoryStream(bytes), "phase2-overlap.csv", bytes.Length), CancellationToken.None);

        Assert.Equal(0, result.RowsInserted);
        Assert.Equal(1, result.RowsDuplicate);
        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuditDbContext(options);
    }
}

