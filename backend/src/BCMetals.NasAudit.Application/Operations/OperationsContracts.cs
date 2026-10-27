using System.ComponentModel.DataAnnotations;
using BCMetals.NasAudit.Application.Alerts;
using BCMetals.NasAudit.Application.Ingestion;
using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Operations;

public sealed record IngestionHealthDto(
    DateTimeOffset? LastBatchUtc,
    long BatchesLast24Hours,
    long AcceptedLast24Hours,
    long DuplicatesLast24Hours,
    long RejectedLast24Hours,
    int PendingEvents);

public sealed record RetentionPolicyDto(
    bool Enabled,
    int HistoricalDays,
    int LiveDays,
    int DemoDays,
    int IngestionMetadataDays,
    int RejectedEventDays,
    int BatchSize,
    int RunAtUtcHour,
    DateTimeOffset? LastRunUtc,
    string? LastRunStatus);

public sealed record RetentionCountDto(DataMode DataMode, DateTimeOffset CutoffUtc, long EligibleRows);

public sealed record RetentionPreviewDto(
    RetentionPolicyDto Policy,
    IReadOnlyList<RetentionCountDto> Events,
    DateTimeOffset IngestionMetadataCutoffUtc,
    long EligibleIngestionBatches,
    DateTimeOffset RejectedEventCutoffUtc,
    long EligibleRejectedEvents);

public sealed record RetentionRunDto(
    Guid RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    long DeletedEvents,
    long DeletedIngestionBatches,
    long DeletedRejectedEvents,
    string Actor);

public sealed record OperationalEventDto(
    DateTimeOffset TimestampUtc,
    string Severity,
    string Code,
    string Message);

public sealed record SystemStatusDto(
    DateTimeOffset CheckedAtUtc,
    string Version,
    long UptimeSeconds,
    string ApiStatus,
    string DatabaseStatus,
    double DatabaseLatencyMs,
    long DatabaseSizeBytes,
    long TotalEvents,
    DateTimeOffset? LastEventUtc,
    CollectorStatusDto Collector,
    IngestionHealthDto Ingestion,
    RetentionPolicyDto Retention,
    IReadOnlyList<AlertCandidate> Alerts,
    IReadOnlyList<OperationalEventDto> RecentErrors);

public sealed record ChangePasswordRequest(
    [param: Required] string CurrentPassword,
    [param: Required, MinLength(14)] string NewPassword);
