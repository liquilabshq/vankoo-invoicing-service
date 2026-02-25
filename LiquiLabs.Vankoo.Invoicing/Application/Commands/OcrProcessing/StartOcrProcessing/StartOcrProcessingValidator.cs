using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.StartOcrProcessing;

public class StartOcrProcessingValidator: AbstractValidator<StartOcrProcessingCommand>
{
    public StartOcrProcessingValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("El InvoiceId no puede estar vacia");
    }
}