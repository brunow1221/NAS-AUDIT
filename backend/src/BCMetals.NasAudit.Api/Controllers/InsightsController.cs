using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Insights;
using BCMetals.NasAudit.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("analytics")]
public sealed class InsightsController(IInsightsService insightsService) : ControllerBase
{
    [HttpGet("api/v1/paths")]
    public async Task<ActionResult<ApiEnvelope<PagedResult<PathSummaryDto>>>> Paths(
        [FromQuery] DataMode mode = DataMode.Historical,
        [FromQuery] string? search = null,
        [FromQuery] string? share = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetPathsAsync(
            new PathQuery(mode, search, share, page, pageSize), cancellationToken);
        return Ok(new ApiEnvelope<PagedResult<PathSummaryDto>>(result));
    }

    [HttpGet("api/v1/paths/history")]
    public async Task<ActionResult<ApiEnvelope<PathTimelineDto>>> PathHistory(
        [FromQuery] string path,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetPathTimelineAsync(path, mode, cancellationToken);
        return result is null ? NotFound() : Ok(new ApiEnvelope<PathTimelineDto>(result));
    }

    [HttpGet("api/v1/smb-users")]
    public async Task<ActionResult<ApiEnvelope<PagedResult<SmbUserSummaryDto>>>> Users(
        [FromQuery] DataMode mode = DataMode.Historical,
        [FromQuery] string? search = null,
        [FromQuery] string? share = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to, mode);
        var result = await insightsService.GetUsersAsync(
            new PathQuery(mode, search, share, page, pageSize, range.FromUtc, range.ToUtc), cancellationToken);
        return Ok(new ApiEnvelope<PagedResult<SmbUserSummaryDto>>(result));
    }

    [HttpGet("api/v1/folders/history")]
    public async Task<ActionResult<ApiEnvelope<FolderHistoryDto>>> FolderHistory(
        [FromQuery] string path,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetFolderHistoryAsync(path, Range(from, to, mode), cancellationToken);
        return result is null ? NotFound() : Ok(new ApiEnvelope<FolderHistoryDto>(result));
    }

    [HttpGet("api/v1/smb-users/{user}/activity")]
    public async Task<ActionResult<ApiEnvelope<SmbUserActivityDto>>> UserActivity(
        string user,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetUserActivityAsync(user, Range(from, to, mode), cancellationToken);
        return result is null ? NotFound() : Ok(new ApiEnvelope<SmbUserActivityDto>(result));
    }

    [HttpGet("api/v1/statistics")]
    public async Task<ActionResult<ApiEnvelope<StatisticsDto>>> Statistics(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetStatisticsAsync(Range(from, to, mode), cancellationToken);
        return Ok(new ApiEnvelope<StatisticsDto>(result));
    }

    [HttpGet("api/v1/activity-groups")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ActivityGroupDto>>>> ActivityGroups(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await insightsService.GetActivityGroupsAsync(Range(from, to, mode), limit, cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<ActivityGroupDto>>(result));
    }

    [HttpGet("api/v1/reports/{kind}")]
    public async Task<IActionResult> Report(
        ReportKind kind,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var report = await insightsService.ExportAsync(kind, Range(from, to, mode), cancellationToken);
        Response.Headers.Append("X-Report-Rows", report.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return File(report.Content, "text/csv; charset=utf-8", report.FileName);
    }

    private static InsightRange Range(DateTimeOffset? from, DateTimeOffset? to, DataMode mode)
    {
        var end = to?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddMinutes(1);
        var start = from?.ToUniversalTime() ?? end.AddDays(-30);
        if (start >= end || end - start > TimeSpan.FromDays(366))
            throw new ArgumentException("El rango debe ser válido y no superar 366 días.");
        return new InsightRange(start, end, mode);
    }
}
