using System.Text.Json;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Operations;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class RetentionService(
    AuditDbContext dbContext,
    IOptions<RetentionOptions> options) : IRetentionService
{
    private static readonly SemaphoreSlim RunGate = new(1, 1);

    public async Task<RetentionPreviewDto> GetPreviewAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var settings = options.Value;
        var eventPolicies = new[]
        {
            (DataMode.Historical, settings.HistoricalDays),
            (DataMode.Live, settings.LiveDays),
            (DataMode.Demo, settings.DemoDays)
        };
        var counts = new List<RetentionCountDto>();
        foreach (var (mode, days) in eventPolicies)
        {
            var cutoff = now.AddDays(-days);
            var count = await dbContext.AuditEvents.AsNoTracking()
                .LongCountAsync(x => x.DataMode == mode && x.EventTimestampUtc < cutoff, cancellationToken);
            counts.Add(new RetentionCountDto(mode, cutoff, count));
        }

        var ingestionCutoff = now.AddDays(-settings.IngestionMetadataDays);
        var rejectedCutoff = now.AddDays(-settings.RejectedEventDays);
        var batches = await dbContext.IngestionBatches.AsNoTracking()
            .LongCountAsync(x => x.ReceivedAtUtc < ingestionCutoff, cancellationToken);
        var rejected = await dbContext.RejectedIngestionEvents.AsNoTracking()
            .LongCountAsync(x => x.ReceivedAtUtc < rejectedCutoff, cancellationToken);
        return new RetentionPreviewDto(
            await PolicyAsync(cancellationToken), counts, ingestionCutoff, batches, rejectedCutoff, rejected);
    }

    public async Task<RetentionRunDto> RunAsync(string actor, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            throw new InvalidOperationException("La retención está desactivada. Defina y apruebe primero la política en .env.");
        if (!await RunGate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Ya existe una ejecución de retención en curso.");

        var started = DateTimeOffset.UtcNow;
        var run = new MaintenanceRun
        {
            Kind = "retention",
            Status = "running",
            Actor = actor[..Math.Min(actor.Length, 256)],
            StartedAtUtc = started
        };
        dbContext.MaintenanceRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        long deletedEvents = 0;
        long deletedBatches = 0;
        long deletedRejected = 0;
        try
        {
            deletedEvents += await DeleteEventsAsync(DataMode.Historical, started.AddDays(-settings.HistoricalDays), settings.BatchSize, cancellationToken);
            deletedEvents += await DeleteEventsAsync(DataMode.Live, started.AddDays(-settings.LiveDays), settings.BatchSize, cancellationToken);
            deletedEvents += await DeleteEventsAsync(DataMode.Demo, started.AddDays(-settings.DemoDays), settings.BatchSize, cancellationToken);
            deletedBatches = await DeleteBatchesAsync(started.AddDays(-settings.IngestionMetadataDays), settings.BatchSize, cancellationToken);
            deletedRejected = await DeleteRejectedAsync(started.AddDays(-settings.RejectedEventDays), settings.BatchSize, cancellationToken);
            run.Status = "completed";
            return Complete(run, deletedEvents, deletedBatches, deletedRejected);
        }
        catch (Exception exception)
        {
            run.Status = "failed";
            run.DetailsJson = JsonSerializer.Serialize(new { error = exception.GetType().Name });
            throw;
        }
        finally
        {
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.AffectedRows = deletedEvents + deletedBatches + deletedRejected;
            if (run.Status == "completed")
            {
                run.DetailsJson = JsonSerializer.Serialize(new
                {
                    deletedEvents,
                    deletedIngestionBatches = deletedBatches,
                    deletedRejectedEvents = deletedRejected
                });
            }
            await dbContext.SaveChangesAsync(CancellationToken.None);
            RunGate.Release();
        }
    }

    private async Task<RetentionPolicyDto> PolicyAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var last = await dbContext.MaintenanceRuns.AsNoTracking().Where(x => x.Kind == "retention")
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return new RetentionPolicyDto(
            settings.Enabled,
            settings.HistoricalDays,
            settings.LiveDays,
            settings.DemoDays,
            settings.IngestionMetadataDays,
            settings.RejectedEventDays,
            settings.BatchSize,
            settings.RunAtUtcHour,
            last?.CompletedAtUtc,
            last?.Status);
    }

    private async Task<long> DeleteEventsAsync(DataMode mode, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        long total = 0;
        while (true)
        {
            var ids = await dbContext.AuditEvents.AsNoTracking()
                .Where(x => x.DataMode == mode && x.EventTimestampUtc < cutoff)
                .OrderBy(x => x.EventTimestampUtc).Select(x => x.Id).Take(batchSize).ToListAsync(cancellationToken);
            if (ids.Count == 0) return total;
            total += await dbContext.AuditEvents.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task<long> DeleteBatchesAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        long total = 0;
        while (true)
        {
            var ids = await dbContext.IngestionBatches.AsNoTracking().Where(x => x.ReceivedAtUtc < cutoff)
                .OrderBy(x => x.ReceivedAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(cancellationToken);
            if (ids.Count == 0) return total;
            total += await dbContext.IngestionBatches.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task<long> DeleteRejectedAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        long total = 0;
        while (true)
        {
            var ids = await dbContext.RejectedIngestionEvents.AsNoTracking().Where(x => x.ReceivedAtUtc < cutoff)
                .OrderBy(x => x.ReceivedAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(cancellationToken);
            if (ids.Count == 0) return total;
            total += await dbContext.RejectedIngestionEvents.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static RetentionRunDto Complete(MaintenanceRun run, long events, long batches, long rejected) =>
        new(run.Id, run.StartedAtUtc, DateTimeOffset.UtcNow, "completed", events, batches, rejected, run.Actor);
}
