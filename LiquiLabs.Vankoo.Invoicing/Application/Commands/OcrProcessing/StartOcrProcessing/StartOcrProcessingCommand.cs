using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.StartOcrProcessing;

public record StartOcrProcessingCommand(string InvoiceId) : IRequest<StartOcrProcessingResponse>;