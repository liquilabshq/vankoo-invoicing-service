using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.CreateInvoice;

public class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, string>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public CreateInvoiceHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<string> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var mypeId = MypeId.Of(request.MypeId);
        
        var document = InvoiceDocument.Upload(
            request.FileName,
            "application/pdf",
            request.FileSizeBytes
        );

        var invoice = Invoice.Create(mypeId, document);
        
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        
        return invoice.Id.Value;
    }
}
