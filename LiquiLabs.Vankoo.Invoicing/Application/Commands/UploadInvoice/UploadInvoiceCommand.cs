using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public record UploadInvoiceCommand : IRequest<string>
{
    public string MypeId { get; init; }
    public string OriginalName { get; init; }
    public string ContentType { get; init; }
    public long FileSizeBytes { get; init; }
    public Stream FileStream { get; init; }
}
