using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

// Implementa INotification para que MediatR sepa que es un evento en memoria
public sealed record InvoiceOcrProcessedDomainEvent(
    Invoice Invoice
) : INotification;