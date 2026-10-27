using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Dashboard;
using BCMetals.NasAudit.Application.Events;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface IAuditReadService
{
    Task<PagedResult<AuditEventDto>> GetEventsAsync(EventQuery query, CancellationToken cancellationToken);
    Task<AuditEventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEventDto>> GetRecentLiveEventsAsync(int limit, CancellationToken cancellationToken);
    Task<EventExport> ExportEventsAsync(EventQuery query, CancellationToken cancellationToken);
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActivityPointDto>> GetActivityAsync(DashboardQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistributionPointDto>> GetDistributionAsync(DashboardQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopUserDto>> GetTopUsersAsync(DashboardQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShareActivityDto>> GetSharesAsync(DashboardQuery query, CancellationToken cancellationToken);
}

