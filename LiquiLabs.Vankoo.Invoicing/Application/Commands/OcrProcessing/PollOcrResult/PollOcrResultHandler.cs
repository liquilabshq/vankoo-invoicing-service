using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.PollOcrResult;

public class PollOcrResultHandler: IRequestHandler<PollOcrResultCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IOcrService _ocrService;
    private readonly ILogger<PollOcrResultHandler> _logger;
    
    public PollOcrResultHandler(
        IInvoiceRepository invoiceRepository, 
        IOcrService ocrService, 
        ILogger<PollOcrResultHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _ocrService = ocrService;
        _logger = logger;
    }
    
    public async Task Handle(PollOcrResultCommand command, CancellationToken cancellationToken)
    {
        var operationId = OcrOperationId.Of(command.OperationId);
        _logger.LogInformation("Polling OCR result for operationId {OperationId}", operationId.Value);
        
        var isCompleted = await _ocrService.IsAnalysisCompletedAsync(operationId, cancellationToken);
        if (!isCompleted) 
        {
            _logger.LogDebug("Operation {OperationId} todavía esta en proceso.", operationId.Value);
            return; 
        }
        
        var result = await _ocrService.GetAnalysisResultAsync(operationId, cancellationToken);
        if (result == null)
        {
            _logger.LogWarning("Se completó el OCR pero no se pudieron obtener los resultados para OperationId {OperationId}", operationId.Value);
            return;
        }
        
        var invoice = await _invoiceRepository.GetByOcrOperationIdAsync(operationId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Se completó el OCR pero no se encontró la factura con OperationId {OperationId}", command.OperationId);
            return;
        }
        
        invoice.RegisterOcrResults(result);
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        
        _logger.LogInformation("Factura {InvoiceId} actualizada con datos del OCR asíncrono", invoice.Id.Value);
    }
}