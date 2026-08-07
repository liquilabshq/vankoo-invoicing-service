using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands;

public record UploadInvoicePruebaCommand(
    string MypeId,
    string LocalFilePath
) : IRequest<string>;