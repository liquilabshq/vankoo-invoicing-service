using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsFunded;

public sealed class MarkInvoiceAsFundedCommandHandler : IRequestHandler<MarkInvoiceAsFundedCommand, Unit>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public MarkInvoiceAsFundedCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Unit> Handle(MarkInvoiceAsFundedCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        // Kafka entrega al menos una vez: un AuctionFullyFunded repetido no debe fallar.
        if (invoice.Status == InvoiceStatus.FUNDED)
            return Unit.Value;

        invoice.MarkFunded();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        return Unit.Value;
    }
}
