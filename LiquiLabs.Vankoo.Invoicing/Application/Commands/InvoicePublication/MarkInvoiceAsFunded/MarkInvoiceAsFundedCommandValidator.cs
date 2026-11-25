using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsFunded;

public sealed class MarkInvoiceAsFundedCommandValidator : AbstractValidator<MarkInvoiceAsFundedCommand>
{
    public MarkInvoiceAsFundedCommandValidator()
    {
        RuleFor(command => command.InvoiceId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El InvoiceId debe ser un UUID válido.");
    }
}
