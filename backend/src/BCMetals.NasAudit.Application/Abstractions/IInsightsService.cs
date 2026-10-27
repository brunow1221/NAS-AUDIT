using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Insights;
using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface IInsightsService
{
    Task<PagedResult<PathSummaryDto>> GetPathsAsync(PathQuery query, CancellationToken cancellationToken);
    Task<PathTimelineDto?> GetPathTimelineAsync(string path, DataMode mode, CancellationToken cancellationToken);
    Task<FolderHistoryDto?> GetFolderHistoryAsync(string path, InsightRange range, CancellationToken cancellationToken);
    Task<PagedResult<SmbUserSummaryDto>> GetUsersAsync(PathQuery query, CancellationToken cancellationToken);
    Task<SmbUserActivityDto?> GetUserActivityAsync(string user, InsightRange range, CancellationToken cancellationToken);
    Task<StatisticsDto> GetStatisticsAsync(InsightRange range, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActivityGroupDto>> GetActivityGroupsAsync(InsightRange range, int limit, CancellationToken cancellationToken);
    Task<InsightExport> ExportAsync(ReportKind kind, InsightRange range, CancellationToken cancellationToken);
}
