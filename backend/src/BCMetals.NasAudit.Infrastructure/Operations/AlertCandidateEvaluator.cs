using BCMetals.NasAudit.Application.Alerts;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class AlertCandidateEvaluator(
    AuditDbContext dbContext,
    IOptions<MonitoringOptions> options) : IAlertCandidateEvaluator
{
    public async Task<IReadOnlyList<AlertCandidate>> EvaluateAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.BehavioralCandidatesEnabled) return [];

        var query = dbContext.AuditEvents.AsNoTracking().Where(x =>
            x.DataMode == DataMode.Live &&
            x.EventTimestampUtc >= fromUtc &&
            x.EventTimestampUtc < toUtc);
        var counts = await query.GroupBy(x => x.InterpretedAction)
            .Select(group => new { Action = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(x => x.Action, x => x.Count, cancellationToken);
        var candidates = new List<AlertCandidate>();
        AddThresholdCandidate(candidates, counts, AuditAction.Deletion, settings.DeletionCandidateThreshold,
            "deletion-volume", "Volumen elevado de eliminaciones", fromUtc, toUtc);
        AddThresholdCandidate(candidates, counts, AuditAction.Write, settings.WriteCandidateThreshold,
            "write-volume", "Volumen elevado de escrituras", fromUtc, toUtc);

        foreach (var prefix in settings.SensitivePathPrefixes.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var normalized = prefix.Trim().Replace('\\', '/').TrimEnd('/');
            var count = await query.LongCountAsync(x =>
                (x.FilePath != null && x.FilePath.StartsWith(normalized)) ||
                (x.SourcePath != null && x.SourcePath.StartsWith(normalized)) ||
                (x.DestinationPath != null && x.DestinationPath.StartsWith(normalized)), cancellationToken);
            if (count == 0) continue;
            candidates.Add(new AlertCandidate(
                "sensitive-path-activity",
                "info",
                "Actividad en ruta sensible configurada",
                fromUtc,
                toUtc,
                count,
                "Hay eventos en una ruta marcada para revisión adicional. Esto no implica actividad maliciosa.",
                new Dictionary<string, string> { ["pathPrefix"] = normalized }));
        }
        return candidates;
    }

    private static void AddThresholdCandidate(
        List<AlertCandidate> target,
        Dictionary<AuditAction, long> counts,
        AuditAction action,
        int threshold,
        string ruleKey,
        string title,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (threshold < 1 || !counts.TryGetValue(action, out var count) || count < threshold) return;
        target.Add(new AlertCandidate(
            ruleKey,
            "warning",
            title,
            fromUtc,
            toUtc,
            count,
            $"El conteo superó el umbral explícito de {threshold}. Requiere revisión humana y no constituye una clasificación de amenaza.",
            new Dictionary<string, string> { ["action"] = action.ToString(), ["threshold"] = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
    }
}
