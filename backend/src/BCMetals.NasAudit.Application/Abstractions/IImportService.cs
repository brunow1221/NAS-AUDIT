using BCMetals.NasAudit.Application.Importing;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface IImportService
{
    Task<CsvPreviewDto> PreviewAsync(ImportRequest request, CancellationToken cancellationToken);
    Task<ImportJobDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportJobDto>> ListAsync(CancellationToken cancellationToken);
}

