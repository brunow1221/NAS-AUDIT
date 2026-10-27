using System.Security.Claims;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/v1/admin/retention")]
public sealed class OperationsController(IRetentionService retentionService) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<ActionResult<ApiEnvelope<RetentionPreviewDto>>> Preview(CancellationToken cancellationToken)
    {
        var result = await retentionService.GetPreviewAsync(cancellationToken);
        return Ok(new ApiEnvelope<RetentionPreviewDto>(result));
    }

    [HttpPost("run")]
    public async Task<ActionResult<ApiEnvelope<RetentionRunDto>>> Run(CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "administrator";
        var result = await retentionService.RunAsync(actor, cancellationToken);
        return Ok(new ApiEnvelope<RetentionRunDto>(result, "Retención completada según la política configurada."));
    }
}
