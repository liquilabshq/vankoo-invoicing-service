using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.StartOcrProcessing;

public class StartOcrProcessingHandler : IRequestHandler<StartOcrProcessingCommand, StartOcrProcessingResponse>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IOcrService _ocrService;   
    private readonly IStorageService _storageService;
    private readonly  ILogger<StartOcrProcessingHandler> _logger;
    
    public StartOcrProcessingHandler(
        IInvoiceRepository invoiceRepository,
        IOcrService ocrService,
        IStorageService storageService,
        ILogger<StartOcrProcessingHandler> logger
        )
    {
        _invoiceRepository = invoiceRepository;
        _storageService = storageService;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<StartOcrProcessingResponse> Handle(StartOcrProcessingCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OCR processing for invoice {InvoiceId}", command.InvoiceId);
        var invoiceId = InvoiceId.Of(command.InvoiceId);

        
        // Obtener el invoice (factura)
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogError("Invoice with ID {InvoiceId} not found", invoiceId.Value);
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        }
        // Cambiar el estado del Invoice a OCR_PROCESSING
        invoice.StartOcrProcessing();
        await _invoiceRepository.SaveAsync(invoice,cancellationToken);
        
        // Iniciar el análisis OCR de forma asíncrona
        try
        {
            await using var fileStream =
                await _storageService.GetFileStreamAsync(invoice.Document.Key, cancellationToken);

            var operationId = await _ocrService.StartAnalysisAsync(fileStream, cancellationToken);
            invoice.SetOcrOperationId(operationId);
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            
            return new StartOcrProcessingResponse(operationId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start async OCR for Invoice {InvoiceId}", command.InvoiceId);
            invoice.Reject(RejectionReason.Create($"Error al iniciar OCR: {ex.Message}", "SYSTEM"));
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            throw;
        }
    }




}