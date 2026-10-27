using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Ingestion;
using BCMetals.NasAudit.Application.Operations;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
public sealed class SystemController(
    AuditDbContext dbContext,
    ICollectorIngestionService ingestionService,
    IOperationsService operationsService) : ControllerBase
{
    [HttpGet("health/live")]
    [AllowAnonymous]
    public IActionResult Live() => Ok(new { status = "ok", service = "nas-audit-api" });

    [HttpGet("health/ready")]
    [AllowAnonymous]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var ready = await dbContext.Database.CanConnectAsync(cancellationToken);
        return ready ? Ok(new { status = "ready" }) : StatusCode(503, new { status = "unavailable" });
    }

    [HttpGet("api/v1/system/status")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<SystemStatusDto>>> Status(CancellationToken cancellationToken)
    {
        var status = await operationsService.GetStatusAsync(cancellationToken);
        return Ok(new ApiEnvelope<SystemStatusDto>(status));
    }

    [HttpGet("api/v1/collector/status")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<CollectorStatusDto>>> Collector(CancellationToken cancellationToken)
    {
        var status = await ingestionService.GetStatusAsync(cancellationToken);
        return Ok(new ApiEnvelope<CollectorStatusDto>(status));
    }

    [HttpGet("api/v1/collector/health")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<CollectorStatusDto>>> CollectorHealth(CancellationToken cancellationToken)
    {
        var status = await ingestionService.GetStatusAsync(cancellationToken);
        return status.Status is "connected" or "active_no_events"
            ? Ok(new ApiEnvelope<CollectorStatusDto>(status))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiEnvelope<CollectorStatusDto>(status));
    }
}

