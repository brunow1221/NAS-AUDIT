using BCMetals.NasAudit.Api.Security;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Ingestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = CollectorAuthenticationDefaults.Scheme)]
[EnableRateLimiting("ingestion")]
[RequestSizeLimit(2 * 1024 * 1024)]
[Route("api/v1/ingestion")]
public sealed class IngestionController(ICollectorIngestionService ingestionService) : ControllerBase
{
    [HttpPost("events")]
    public async Task<ActionResult<ApiEnvelope<IngestionBatchResult>>> Events(
        IngestionBatchRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCollectorMatches(request.CollectorId);
        var result = await ingestionService.IngestAsync(request, cancellationToken);
        return Ok(new ApiEnvelope<IngestionBatchResult>(result));
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ApiEnvelope<object>>> Heartbeat(
        CollectorHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCollectorMatches(request.CollectorId);
        await ingestionService.RecordHeartbeatAsync(request, cancellationToken);
        return Ok(new ApiEnvelope<object>(new { accepted = true }));
    }

    private void EnsureCollectorMatches(string collectorId)
    {
        var authenticatedId = User.FindFirst("collector_id")?.Value;
        if (!string.Equals(authenticatedId, collectorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("El identificador autenticado no coincide con el payload.");
    }
}
