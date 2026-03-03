using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.CreateInvoice;

public record CreateInvoiceCommand(
    string MypeId,
    string FileName,
    long FileSizeBytes
) : IRequest<string>;
