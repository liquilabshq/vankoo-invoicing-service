using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

public record ProcessOcrSynchronouslyCommand(string InvoiceId) : IRequest;