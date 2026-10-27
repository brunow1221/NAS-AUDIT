using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Importing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanImport")]
[Route("api/v1/imports")]
public sealed class ImportsController(IImportService importService) : ControllerBase
{
    private const int MultipartLimit = 21 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ImportJobDto>>>> List(CancellationToken cancellationToken)
    {
        var result = await importService.ListAsync(cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<ImportJobDto>>(result));
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartLimit)]
    [RequestSizeLimit(MultipartLimit)]
    public async Task<ActionResult<ApiEnvelope<ImportJobDto>>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) return BadRequest(new ProblemDetails { Title = "Seleccione un CSV con contenido." });
        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(new ImportRequest(stream, file.FileName, file.Length), cancellationToken);
        return Ok(new ApiEnvelope<ImportJobDto>(result, "Importación procesada."));
    }

    [HttpPost("preview")]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartLimit)]
    [RequestSizeLimit(MultipartLimit)]
    public async Task<ActionResult<ApiEnvelope<CsvPreviewDto>>> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) return BadRequest(new ProblemDetails { Title = "Seleccione un CSV con contenido." });
        await using var stream = file.OpenReadStream();
        var result = await importService.PreviewAsync(new ImportRequest(stream, file.FileName, file.Length), cancellationToken);
        return Ok(new ApiEnvelope<CsvPreviewDto>(result));
    }
}

