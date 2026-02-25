using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.PollOcrResult;

public class PollOcrResultValidator : AbstractValidator<PollOcrResultCommand>
{
    public PollOcrResultValidator()
    {
        RuleFor(x => x.OperationId)
            .NotEmpty().WithMessage("El OperationId de Azure es obligatorio para verificar el estado.");
    }
}