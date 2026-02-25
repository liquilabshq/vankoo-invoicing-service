using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.PollOcrResult;

public record PollOcrResultCommand(string OperationId) : IRequest;