using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.EventHandlers;


public sealed class InvoiceOcrProcessedEventHandler
    : INotificationHandler<InvoiceOcrProcessedDomainEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<InvoiceOcrProcessedEventHandler> _logger;

    public InvoiceOcrProcessedEventHandler(
        IEventBus eventBus,
        ILogger<InvoiceOcrProcessedEventHandler> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task Handle(
        InvoiceOcrProcessedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        var invoice = notification.Invoice;
        
        // ORIGINAL
        // var integrationEvent = new InvoiceOcrProcessedIntegrationEvent(
        //     EventId: Guid.NewGuid(),
        //     OccurredOn: DateTime.UtcNow,
        //     InvoiceId: invoice.Id.Value,
        //     MypeId: invoice.MypeId.ToString(),
        //     PayerRuc: invoice.PayerData.Ruc.Value,
        //     PayerName: invoice.PayerData.GetDisplayName(),
        //     DueDate: invoice.Metadata.DueDate,
        //     Currency: invoice.Metadata.Currency.ToString(),
        //     TotalAmount: invoice.TotalAmount.Amount
        // );
        
        // PROBAR
        var integrationEvent = new InvoiceOcrProcessedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredOn: DateTime.UtcNow,
            InvoiceId: invoice.Id?.Value ?? "00000000-0000-0000-0000-000000000",
            MypeId: invoice.MypeId?.ToString() ?? "UNKNOWN",
            PayerRuc: invoice.PayerData?.Ruc?.Value ?? "00000000000",
            PayerName: invoice.PayerData?.GetDisplayName() ?? "SIN NOMBRE",
            DueDate: invoice.Metadata?.DueDate ?? DateTime.UtcNow,
            Currency: invoice.Metadata?.Currency.ToString() ?? "PEN",
            TotalAmount: invoice.TotalAmount?.Amount ?? 0m
        );

        _logger.LogInformation(
            "Publicando integration event {EventId} para factura {InvoiceId}",
            integrationEvent.EventId, integrationEvent.InvoiceId);

        await _eventBus.PublishAsync(integrationEvent, cancellationToken);
    }
}