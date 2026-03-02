using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public class UploadInvoiceCommandHandler : IRequestHandler<UploadInvoiceCommand, string>
{
    private readonly IStorageService _storageService;
    private readonly IInvoiceRepository _repository;

    public UploadInvoiceCommandHandler(IStorageService storageService, IInvoiceRepository repository)
    {
        _storageService = storageService;
        _repository = repository;
    }

    public async Task<string> Handle(UploadInvoiceCommand command, CancellationToken ct)
    {
        var document = InvoiceDocument.Upload(command.OriginalName, command.ContentType, command.FileSizeBytes);
        var invoice = Invoice.Create(MypeId.Of(command.MypeId), document);

        await _storageService.UploadAsync(invoice.Document.Key.Value, command.FileStream, command.ContentType, ct);
        await _repository.SaveAsync(invoice, ct);

        return invoice.Id.Value;
    }
}
