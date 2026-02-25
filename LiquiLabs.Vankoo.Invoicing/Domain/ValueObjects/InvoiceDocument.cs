using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceDocument
{
    public FileKey Key { get; init; }
    public string OriginalName { get; init; }
    public string ContentType { get; init; }
    public long FileSizeBytes { get; init; }

    private InvoiceDocument() { }

    public static InvoiceDocument Upload(string originalName, string contentType, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            throw new ArgumentException("File name cannot be empty", nameof(originalName));

        if (contentType != "application/pdf")
            throw new InvoiceDomainException("Invoice must be a PDF file");

        if (fileSizeBytes <= 0)
            throw new InvoiceDomainException("File size must be greater than zero");

        return new InvoiceDocument
        {
            Key = FileKey.Generate(),
            OriginalName = originalName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes
        };
    }
}
