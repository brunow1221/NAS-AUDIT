using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Dashboard;

public sealed record DashboardSummaryDto(
    long TotalEvents,
    long EventsLast24Hours,
    int ActiveAccounts,
    long CreatedFiles,
    long ModifiedFiles,
    long Deletions,
    long Renames,
    long Connections,
    DateTimeOffset? LastEventUtc,
    string CollectorStatus);

public sealed record ActivityPointDto(DateTimeOffset BucketUtc, long Count);
public sealed record DistributionPointDto(AuditAction Action, long Count);
public sealed record TopUserDto(string User, long Count);
public sealed record ShareActivityDto(string Share, long Count);

public sealed record DashboardQuery(DateTimeOffset FromUtc, DateTimeOffset ToUtc, DataMode DataMode);

