using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Services;

public class OcrProcessingService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IOcrService _ocrService;
    private readonly ILogger<OcrProcessingService> _logger; // Para detectar errores
    
    public OcrProcessingService(
        IInvoiceRepository invoiceRepository,
        IOcrService ocrService,
        ILogger<OcrProcessingService> logger)
    {
        ArgumentNullException.ThrowIfNull(ocrService);
        ArgumentNullException.ThrowIfNull(invoiceRepository);
        ArgumentNullException.ThrowIfNull(logger);
        
        _invoiceRepository = invoiceRepository;
        _ocrService = ocrService;
        _logger = logger;
    }
    
    // Inicia el procesamiento del OCR de forma asíncrona
    public async Task StartOcrProcessingAsync(
        InvoiceId invoiceId, 
        DocumentUrl fileUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OCR processing for invoice {InvoiceId}", invoiceId.Value);
        
        // Obtener el invoice (factura)
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogError("Invoice with ID {InvoiceId} not found", invoiceId.Value);
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        }
        
        // Cambiar el estado del Invoice a OCR_PROCESSING
        invoice.StartOcrProcessing();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        
        // Iniciar el análisis OCR de forma asíncrona
        try
        {
            var operationId = await _ocrService.StartAnalysisAsync(fileUrl, cancellationToken);

            invoice.SetOcrOperationId(operationId);
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);

            _logger.LogInformation(
                "OCR analysis started for invoice {InvoiceId} with operationId {OperationId}",
                invoiceId.Value,
                operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OCR analysis for invoice {InvoiceId}", invoiceId.Value);

            // Rechazar la factura si el OCR falla al iniciar
            invoice.Reject(
                RejectionReason.Create($"Failed to start OCR: {ex.Message}",rejectedBy: "SYSTEM"));
            
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            throw;
        }
        
    }
    
    // Método para hacer polling del resultado del OCR
    // Consultar periódicamente si el OCR ha terminado y obtener el resultado
    public async Task PollAndProcessOcrResultAsync(
        OcrOperationId operationId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Polling OCR result for operation {OperationId}", operationId);
        
        // Verificar si el análisis OCR ha terminado
        var isCompleted = await _ocrService.IsAnalysisCompletedAsync(operationId, cancellationToken);
        if (!isCompleted)
        {
             _logger.LogDebug("OCR analysis {OperationId} is still processing", operationId);
             return; 
        }
        
        // Obtener el resultado del OCR
        var result = await _ocrService.GetAnalysisResultAsync(operationId, cancellationToken);
        if (result == null)
        {
            _logger.LogWarning("OCR analysis {OperationId} completed but no result found", operationId);
            return;
        }
        
        // Buscar la factura asociada al operationId
        var invoice = await _invoiceRepository.GetByOcrOperationIdAsync(operationId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogError("Invoice with operationId {OperationId} not found", operationId);
            return;
        }
        
        // Registrar el resultado del OCR en la factura
        try
        {
            invoice.RegisterOcrResults(result);
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            
            _logger.LogInformation(
                "OCR results successfully registered for invoice {InvoiceId}",
                invoice.Id.Value);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Failed to register OCR results for invoice {InvoiceId}",
                invoice.Id.Value);
            throw;
        }
    }

    // Método para procesar el OCR de forma sincrónica (para facturas pequeñas)
    public async Task ProcessOcrSynchronouslyAsync(
        InvoiceId invoiceId, 
        DocumentUrl fileUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing OCR synchronously for invoice {InvoiceId}",
            invoiceId.Value);
        
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice == null)
        {
            throw new InvalidOperationException($"Invoice {invoiceId.Value} not found");
        }
        
        invoice.StartOcrProcessing();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        
        try
        {
            var result = await _ocrService.ExtractInvoiceDataAsync(fileUrl, cancellationToken);
            invoice.RegisterOcrResults(result);
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            
            _logger.LogInformation(
                "OCR completed successfully for invoice {InvoiceId}",
                invoiceId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR processing failed for invoice {InvoiceId}", invoiceId.Value);
            
            invoice.Reject(RejectionReason.Create(
                $"OCR processing failed: {ex.Message}",
                rejectedBy: "SYSTEM"));
            
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            throw;
        }
        
        
    }
    
}