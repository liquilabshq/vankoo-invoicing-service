using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Interfaces;

public interface IOcrService
{
    // Solo para procesar facturas pequeñas (se hará de forma sincrona)
    Task<OcrExtractionResult> ExtractInvoiceDataAsync(Stream documentStream, CancellationToken cancellationToken = default);
    
    // Para procesar facturas grandes (se hará de forma asincrona)
    Task<OcrOperationId> StartAnalysisAsync(Stream documentStream, CancellationToken cancellationToken = default);
    
    Task<bool>IsAnalysisCompletedAsync(OcrOperationId operationId, CancellationToken cancellationToken = default);
    
    Task<OcrExtractionResult?> GetAnalysisResultAsync(OcrOperationId operationId, CancellationToken cancellationToken = default);
}