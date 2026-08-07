using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Files;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands;

public class UploadInvoicePruebaHandler : IRequestHandler<UploadInvoicePruebaCommand, string>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStorageService _storageService;
    private readonly InvoiceFileInspector _fileInspector;

    public UploadInvoicePruebaHandler(
        IInvoiceRepository invoiceRepository, 
        IStorageService storageService,
        InvoiceFileInspector fileInspector)
    {
        _invoiceRepository = invoiceRepository;
        _storageService = storageService;
        _fileInspector = fileInspector;
    }

    public async Task<string> Handle(UploadInvoicePruebaCommand command, CancellationToken cancellationToken)
    {
        if (!File.Exists(command.LocalFilePath))
            throw new FileNotFoundException($"El archivo no existe en la ruta: {command.LocalFilePath}");

        var fileInfo = new FileInfo(command.LocalFilePath);
        var mypeId = MypeId.Of(command.MypeId); 
        
        // 1. Creamos el Documento en el Dominio
        var contentType = Path.GetExtension(command.LocalFilePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => throw new InvalidValueException(
                "INVALID_CONTENT_TYPE",
                "Invoice must be a PDF, JPEG or PNG file")
        };

        await using var stream = new FileStream(command.LocalFilePath, FileMode.Open, FileAccess.Read);
        var contentHash = await _fileInspector.ValidateAndHashAsync(stream, contentType, cancellationToken);
        if (await _invoiceRepository.ExistsByContentHashAsync(contentHash, cancellationToken))
        {
            throw new InvalidValueException(
                "DUPLICATE_INVOICE_FILE",
                "This invoice file has already been uploaded");
        }

        var document = InvoiceDocument.Upload(fileInfo.Name, contentType, fileInfo.Length, contentHash);
        
        // 2. Creamos la Factura (Nace en UPLOADED)
        var invoice = Invoice.Create(mypeId, document);

        // 3. Abrimos el archivo de tu PC y lo pasamos al Storage Service
        await _storageService.UploadAsync(document.Key.Value, stream, contentType, cancellationToken);

        // 4. Guardamos en MongoDB
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);

        return invoice.Id.Value;
    }
}
