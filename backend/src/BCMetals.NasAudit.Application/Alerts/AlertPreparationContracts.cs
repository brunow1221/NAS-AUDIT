namespace BCMetals.NasAudit.Application.Alerts;

/// <summary>
/// Advisory observation derived from explicit operational thresholds. A candidate is
/// evidence for human review, not a classification of malicious behaviour.
/// </summary>
public sealed record AlertCandidate(
    string RuleKey,
    string Severity,
    string Title,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    long ObservedCount,
    string Explanation,
    IReadOnlyDictionary<string, string> Dimensions);

public interface IAlertCandidateEvaluator
{
    Task<IReadOnlyList<AlertCandidate>> EvaluateAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
