using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.EventHandlers;

public sealed class InvoiceCreatedEventHandler : INotificationHandler<InvoiceCreatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoiceCreatedEventHandler> _logger;

    public InvoiceCreatedEventHandler(IMediator mediator, ILogger<InvoiceCreatedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(InvoiceCreatedEvent notification, CancellationToken ct)
    {
        var invoiceId = notification.Invoice.Id.Value;
        _logger.LogInformation("Factura {InvoiceId} creada. Iniciando OCR síncrono.", invoiceId);

        await _mediator.Send(new ProcessOcrSynchronouslyCommand(invoiceId), ct);
    }
}
