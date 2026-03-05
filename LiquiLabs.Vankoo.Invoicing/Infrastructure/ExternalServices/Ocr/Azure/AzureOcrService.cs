using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure;


public class AzureOcrService : IOcrService
{
    private readonly DocumentAnalysisClient _client;
    private readonly AzureOcrMapper _mapper;
    private readonly ILogger<AzureOcrService> _logger;

    public AzureOcrService(
        IOptions<AzureOcrSettings> settings,
        AzureOcrMapper mapper,
        ILogger<AzureOcrService> logger)
    {
        var settings1 = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        settings1.Validate();

        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Crear cliente de Azure Form Recognizer
        var credential = new AzureKeyCredential(settings1.ApiKey);
        _client = new DocumentAnalysisClient(new Uri(settings1.Endpoint), credential);
    }

    /// Extrae datos de forma SÍNCRONA (espera el resultado completo).
    /// Usar solo para facturas pequeñas (< 2MB, < 5 páginas)
    
    public async Task<OcrExtractionResult> ExtractInvoiceDataAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        _logger.LogInformation("Starting SYNCHRONOUS OCR analysis from stream.");

        try
        {
            // Usamos AnalyzeDocumentAsync con el Stream
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-invoice", // Modelo pre-entrenado de Azure para facturas
                documentStream,
                cancellationToken: cancellationToken);

            var result = operation.Value;

            _logger.LogInformation(
                "OCR analysis completed. Documents found: {DocumentCount}",
                result.Documents.Count);

            // Mapear respuesta de Azure → Domain Value Objects
            return _mapper.MapToOcrExtractionResult(result);
        }
        catch (RequestFailedException ex) 
        {
            if (ex.Status == 429) 
                throw new OcrProcessingException("Límite de peticiones a Azure excedido.", OcrErrorCode.RateLimitExceeded, isTransient: true, ex);
            
            if (ex.Status == 400 || ex.Status == 415) 
                throw new OcrInvalidDocumentException("El documento no es válido, está borroso o tiene un formato no soportado.", ex);
            
            if (ex.Status >= 500) 
                throw new OcrProcessingException("El servicio de OCR de Azure está fallando.", OcrErrorCode.ServiceError, isTransient: true, ex);

            throw new OcrProcessingException($"Fallo desconocido en OCR: {ex.Message}", OcrErrorCode.Unknown, isTransient: false, ex);
        }
    }

    /// Inicia el análisis de forma ASÍNCRONA y devuelve un operationId.
    /// Usar para facturas grandes (> 2MB, > 5 páginas).

    public async Task<OcrOperationId> StartAnalysisAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        _logger.LogInformation("Starting ASYNCHRONOUS OCR analysis from stream.");

        try
        {
            // Usamos AnalyzeDocumentAsync con el Stream
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Started,
                "prebuilt-invoice",
                documentStream,
                cancellationToken: cancellationToken);

            var operationId = operation.Id;

            _logger.LogInformation(
                "OCR analysis started successfully. OperationId: {OperationId}",
                operationId);

            return OcrOperationId.Of(operationId);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 429) 
                throw new OcrProcessingException("Límite de peticiones a Azure excedido.", OcrErrorCode.RateLimitExceeded, isTransient: true, ex);
            
            if (ex.Status == 400 || ex.Status == 415) 
                throw new OcrInvalidDocumentException("El archivo no es válido o no se puede procesar.", ex);

            if (ex.Status >= 500) 
                throw new OcrProcessingException("El servicio de OCR de Azure está fallando.", OcrErrorCode.ServiceError, isTransient: true, ex);

            throw new OcrProcessingException($"No se pudo iniciar el análisis OCR: {ex.Message}", OcrErrorCode.Unknown, isTransient: false, ex);
        }
    }

    /// Verifica si el análisis ha completado.
    
    public async Task<bool> IsAnalysisCompletedAsync(
        OcrOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationId);

        _logger.LogDebug(
            "Checking completion status for operation {OperationId}",
            operationId.Value);

        try
        {
            // Crear una instancia de AnalyzeDocumentOperation con el operationId
            var operation = new AnalyzeDocumentOperation(operationId.Value, _client);

            // Actualizar el estado de la operación
            await operation.UpdateStatusAsync(cancellationToken);

            var isCompleted = operation.HasCompleted;

            _logger.LogDebug(
                "Operation {OperationId} status: {Status}",
                operationId.Value,
                isCompleted ? "Completed" : "Still processing");

            return isCompleted;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(ex, "Operation {OperationId} not found (404). It may have expired.", operationId.Value);
            return false;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to check status for operation {OperationId}: {StatusCode}", operationId.Value, ex.Status);
            return false;
        }
    }

    
    /// Obtiene el resultado de un análisis previamente iniciado.
    /// Retorna null si el análisis aún no ha completado.
    
    public async Task<OcrExtractionResult?> GetAnalysisResultAsync(
        OcrOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationId);

        _logger.LogDebug(
            "Getting OCR analysis result for operation {OperationId}",
            operationId.Value);

        try
        {
            // Crear instancia de la operación
            var operation = new AnalyzeDocumentOperation(operationId.Value, _client);

            // Actualizar el estado
            await operation.UpdateStatusAsync(cancellationToken);

            // Verificar si ha completado
            if (!operation.HasCompleted)
            {
                _logger.LogDebug(
                    "OCR analysis {OperationId} has not completed yet",
                    operationId.Value);
                
                return null;
            }

            // Verificar si tiene valor
            if (!operation.HasValue)
            {
                _logger.LogWarning(
                    "OCR analysis {OperationId} completed but has no value",
                    operationId.Value);
                
                return null;
            }

            var result = operation.Value;

            _logger.LogInformation(
                "OCR analysis {OperationId} completed successfully. Documents: {DocumentCount}",
                operationId.Value,
                result.Documents.Count);

            // Mapear respuesta de Azure → Domain Value Objects
            return _mapper.MapToOcrExtractionResult(result);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 404)
            {
                throw new OcrOperationNotFoundException("La operación de OCR no existe o ha expirado. Intente subir la factura nuevamente.", ex);
            }

            throw new OcrProcessingException($"No se pudo obtener el resultado del OCR: {ex.Message}", OcrErrorCode.Unknown, isTransient: false, ex);
        }
    }
}
