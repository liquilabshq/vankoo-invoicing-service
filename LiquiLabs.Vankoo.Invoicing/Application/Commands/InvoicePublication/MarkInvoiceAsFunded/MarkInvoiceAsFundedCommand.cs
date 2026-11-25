using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsFunded;

// IRequest<Unit> y no IRequest: ValidationBehavior solo se engancha a IRequest<TResponse>.
public sealed record MarkInvoiceAsFundedCommand(string InvoiceId) : IRequest<Unit>;
