using System.Data;
using System.Diagnostics;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Alerts;
using BCMetals.NasAudit.Application.Ingestion;
using BCMetals.NasAudit.Application.Operations;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class OperationsService(
    AuditDbContext dbContext,
    ICollectorIngestionService ingestionService,
    IAlertCandidateEvaluator alertEvaluator,
    IOperationalEventBuffer operationalEvents,
    IOptions<RetentionOptions> retentionOptions,
    IOptions<MonitoringOptions> monitoringOptions) : IOperationsService
{
    public async Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var connected = await dbContext.Database.CanConnectAsync(cancellationToken);
        stopwatch.Stop();
        if (!connected)
        {
            return new SystemStatusDto(
                checkedAt,
                Version(),
                Environment.TickCount64 / 1000,
                "operational",
                "unavailable",
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                0,
                null,
                new CollectorStatusDto("database_unavailable", "Base de datos no disponible", null, null, null, null, 0, null),
                new IngestionHealthDto(null, 0, 0, 0, 0, 0),
                Policy(null),
                [OperationalAlert("database-unavailable", "critical", "Base de datos no disponible", checkedAt, "PostgreSQL no respondió a la comprobación de disponibilidad.")],
                operationalEvents.Snapshot());
        }

        var since = checkedAt.AddHours(-24);
        var totalEvents = await dbContext.AuditEvents.AsNoTracking().LongCountAsync(cancellationToken);
        var lastEvent = await dbContext.AuditEvents.AsNoTracking().MaxAsync(x => (DateTimeOffset?)x.EventTimestampUtc, cancellationToken);
        var lastBatch = await dbContext.IngestionBatches.AsNoTracking()
            .OrderByDescending(x => x.ReceivedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var batchRows = await dbContext.IngestionBatches.AsNoTracking().Where(x => x.ReceivedAtUtc >= since).ToListAsync(cancellationToken);
        var rejected24Hours = await dbContext.RejectedIngestionEvents.AsNoTracking()
            .LongCountAsync(x => x.ReceivedAtUtc >= since, cancellationToken);
        var collector = await ingestionService.GetStatusAsync(cancellationToken);
        var lastRetention = await dbContext.MaintenanceRuns.AsNoTracking().Where(x => x.Kind == "retention")
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var databaseSize = await DatabaseSizeAsync(cancellationToken);
        var ingestion = new IngestionHealthDto(
            lastBatch?.ReceivedAtUtc,
            batchRows.Count,
            batchRows.Sum(x => (long)x.Accepted),
            batchRows.Sum(x => (long)x.Duplicates),
            rejected24Hours,
            collector.QueueDepth);
        var alerts = await BuildAlertsAsync(checkedAt, collector, rejected24Hours, cancellationToken);
        return new SystemStatusDto(
            checkedAt,
            Version(),
            Environment.TickCount64 / 1000,
            "operational",
            "operational",
            stopwatch.Elapsed.TotalMilliseconds,
            databaseSize,
            totalEvents,
            lastEvent,
            collector,
            ingestion,
            Policy(lastRetention),
            alerts,
            operationalEvents.Snapshot());
    }

    private async Task<IReadOnlyList<AlertCandidate>> BuildAlertsAsync(
        DateTimeOffset now,
        CollectorStatusDto collector,
        long rejected24Hours,
        CancellationToken cancellationToken)
    {
        var settings = monitoringOptions.Value;
        var alerts = new List<AlertCandidate>();
        if (collector.Status is "disconnected" or "source_unavailable" or "error")
        {
            alerts.Add(OperationalAlert(
                "collector-health",
                "critical",
                "Cobertura de recolección interrumpida",
                now,
                collector.Label));
        }
        else if (collector.Status == "recovering")
        {
            alerts.Add(OperationalAlert(
                "collector-recovering",
                "warning",
                "Recolector recuperando datos",
                now,
                collector.Message ?? collector.Label));
        }
        if (collector.QueueDepth >= settings.QueueDepthWarning)
        {
            alerts.Add(new AlertCandidate(
                "collector-queue",
                "warning",
                "Cola del recolector elevada",
                now.AddMinutes(-5),
                now,
                collector.QueueDepth,
                "La cola superó el umbral operativo. Esto puede indicar sincronización atrasada.",
                new Dictionary<string, string> { ["threshold"] = settings.QueueDepthWarning.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }
        if ((collector.Status is "connected" or "active_no_events") &&
            collector.LastEventUtc is not null &&
            collector.LastEventUtc < now.AddHours(-settings.NoEventWarningHours))
        {
            alerts.Add(OperationalAlert(
                "no-recent-events",
                "info",
                "Sin actividad SMB reciente",
                now,
                $"El recolector continúa saludable, pero no registra eventos desde {collector.LastEventUtc:O}."));
        }
        if (rejected24Hours >= settings.RejectedEventsWarning24Hours)
        {
            alerts.Add(new AlertCandidate(
                "rejected-ingestion",
                "warning",
                "Eventos de ingesta rechazados",
                now.AddHours(-24),
                now,
                rejected24Hours,
                "Existen mensajes que no cumplieron el formato esperado y conservaron su evidencia para diagnóstico.",
                new Dictionary<string, string> { ["threshold"] = settings.RejectedEventsWarning24Hours.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        var window = TimeSpan.FromMinutes(settings.BehavioralWindowMinutes);
        alerts.AddRange(await alertEvaluator.EvaluateAsync(now - window, now, cancellationToken));
        return alerts.OrderBy(x => SeverityOrder(x.Severity)).ThenBy(x => x.RuleKey).ToList();
    }

    private async Task<long> DatabaseSizeAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_database_size(current_database())";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private RetentionPolicyDto Policy(MaintenanceRun? last)
    {
        var settings = retentionOptions.Value;
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

    private static AlertCandidate OperationalAlert(string key, string severity, string title, DateTimeOffset now, string explanation) =>
        new(key, severity, title, now, now, 1, explanation, new Dictionary<string, string>());

    private static int SeverityOrder(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2
    };

    private static string Version() => typeof(OperationsService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
}
