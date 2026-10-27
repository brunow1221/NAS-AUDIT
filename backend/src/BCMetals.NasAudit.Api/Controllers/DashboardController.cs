using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Dashboard;
using BCMetals.NasAudit.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IAuditReadService auditReadService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiEnvelope<DashboardSummaryDto>>> Summary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var query = Range(from, to, mode);
        return Ok(new ApiEnvelope<DashboardSummaryDto>(await auditReadService.GetSummaryAsync(query, cancellationToken)));
    }

    [HttpGet("activity")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ActivityPointDto>>>> Activity(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await auditReadService.GetActivityAsync(Range(from, to, mode), cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<ActivityPointDto>>(result));
    }

    [HttpGet("distribution")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<DistributionPointDto>>>> Distribution(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await auditReadService.GetDistributionAsync(Range(from, to, mode), cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<DistributionPointDto>>(result));
    }

    [HttpGet("top-users")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<TopUserDto>>>> TopUsers(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await auditReadService.GetTopUsersAsync(Range(from, to, mode), cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<TopUserDto>>(result));
    }

    [HttpGet("shares")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ShareActivityDto>>>> Shares(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DataMode mode = DataMode.Historical,
        CancellationToken cancellationToken = default)
    {
        var result = await auditReadService.GetSharesAsync(Range(from, to, mode), cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<ShareActivityDto>>(result));
    }

    private static DashboardQuery Range(DateTimeOffset? from, DateTimeOffset? to, DataMode mode)
    {
        var end = to?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddMinutes(1);
        var start = from?.ToUniversalTime() ?? end.AddDays(-7);
        if (start >= end || end - start > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("El rango debe ser válido y no superar 366 días.");
        }
        return new DashboardQuery(start, end, mode);
    }
}

