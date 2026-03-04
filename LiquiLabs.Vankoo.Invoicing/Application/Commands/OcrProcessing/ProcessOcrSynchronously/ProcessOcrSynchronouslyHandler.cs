using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

public class ProcessOcrSynchronouslyHandler : IRequestHandler<ProcessOcrSynchronouslyCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IOcrService _ocrService;
    private readonly IStorageService _storageService;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessOcrSynchronouslyHandler> _logger;

    public ProcessOcrSynchronouslyHandler(
        IInvoiceRepository invoiceRepository, 
        IOcrService ocrService, 
        IStorageService storageService, 
        IMediator mediator,
        ILogger<ProcessOcrSynchronouslyHandler> logger)
    {
        _invoiceRepository = invoiceRepository; 
        _ocrService = ocrService;
        _storageService = storageService; 
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(ProcessOcrSynchronouslyCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice == null) throw new InvoiceNotFoundException(invoiceId.Value);

        invoice.StartOcrProcessing();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);

        try
        {
            await using var fileStream = await _storageService.GetFileStreamAsync(invoice.Document.Key, cancellationToken);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            var result = await _ocrService.ExtractInvoiceDataAsync(memoryStream, cancellationToken);
            
            invoice.RegisterOcrResults(result);
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            
            // Publicar Domain Event -> MediatR lo enruta al InvoiceOcrProcessedEventHandler → Kafka
            await _mediator.Publish(
                new InvoiceOcrProcessedDomainEvent(invoice),
                cancellationToken);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR sync failed for invoice {InvoiceId}", command.InvoiceId);
            invoice.Reject(RejectionReason.Create($"Fallo procesamiento OCR síncrono: {ex.Message}", "SYSTEM"));
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
            throw;
        }
    }
}