using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public class UploadInvoiceCommandValidator : AbstractValidator<UploadInvoiceCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public UploadInvoiceCommandValidator()
    {
        RuleFor(x => x.MypeId)
            .NotEmpty().WithMessage("El MypeId es requerido.");

        RuleFor(x => x.OriginalName)
            .NotEmpty().WithMessage("El nombre del archivo es requerido.");

        RuleFor(x => x.ContentType)
            .Must(ct => ct == "application/pdf")
            .WithMessage("Solo se permiten archivos PDF.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("El archivo no puede estar vacío.")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage("El archivo no puede superar los 10MB.");
    }
}
