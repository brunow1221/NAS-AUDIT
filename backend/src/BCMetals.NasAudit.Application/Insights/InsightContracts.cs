using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Insights;

public sealed record InsightRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc, DataMode DataMode);

public sealed record PathQuery(
    DataMode DataMode,
    string? Search = null,
    string? Share = null,
    int Page = 1,
    int PageSize = 25,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record PathSummaryDto(
    string Path,
    string? Share,
    long EventCount,
    int AccountCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? CreatedAtUtc);

public sealed record PathTimelineDto(
    PathSummaryDto Summary,
    bool PossiblyDeleted,
    bool RenamedOrMovedAway,
    string? LastKnownOperation,
    IReadOnlyList<AuditEventDto> Events);

public sealed record SmbUserSummaryDto(
    string User,
    long EventCount,
    int IpCount,
    int ShareCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    string? LastClientIp);

public sealed record SmbUserActivityDto(
    SmbUserSummaryDto Summary,
    IReadOnlyList<NamedCountDto> Actions,
    IReadOnlyList<NamedCountDto> Shares,
    IReadOnlyList<NamedCountDto> ClientIps,
    IReadOnlyList<DatedCountDto> ActivityByDay,
    IReadOnlyList<AuditEventDto> RecentEvents);

public sealed record NamedCountDto(string Name, long Count);
public sealed record DatedCountDto(DateTimeOffset DayUtc, long Count);
public sealed record DatedActionCountDto(DateTimeOffset DayUtc, AuditAction Action, long Count);

public sealed record FolderSummaryDto(
    string Path,
    string Name,
    string? Share,
    long EventCount,
    int AccountCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? CreatedAtUtc);

public sealed record FolderHistoryDto(
    FolderSummaryDto Summary,
    bool PathSampleTruncated,
    IReadOnlyList<FolderSummaryDto> Subfolders,
    IReadOnlyList<NamedCountDto> Actions,
    IReadOnlyList<NamedCountDto> Accounts,
    IReadOnlyList<AuditEventDto> RecentEvents);

public sealed record FolderActivityDto(
    string Folder,
    string? Share,
    long EventCount,
    int AccountCount,
    DateTimeOffset LastSeenUtc);

public sealed record StatisticsDto(
    long TotalEvents,
    int ActiveDays,
    double AverageEventsPerActiveDay,
    bool FolderAnalysisTruncated,
    IReadOnlyList<DatedActionCountDto> Trend,
    IReadOnlyList<NamedCountDto> Extensions,
    IReadOnlyList<NamedCountDto> Results,
    IReadOnlyList<FolderActivityDto> Folders);

public sealed record ActivityGroupDto(
    string GroupId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string? User,
    string? ClientIp,
    string? Share,
    string? Path,
    int EventCount,
    IReadOnlyList<string> Operations,
    IReadOnlyList<Guid> EventIds);

public enum ReportKind
{
    Events,
    Users,
    Paths,
    ActivityGroups
}

public sealed record InsightExport(byte[] Content, string FileName, long RowCount);
