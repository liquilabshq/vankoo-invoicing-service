using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public sealed class UploadInvoiceCommandValidator : AbstractValidator<UploadInvoiceCommand>
{
    private static readonly string[] SupportedContentTypes =
        ["application/pdf", "image/jpeg", "image/png"];

    public UploadInvoiceCommandValidator()
    {
        RuleFor(command => command.MypeId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El MypeId debe ser un UUID válido.");

        RuleFor(command => command.OriginalName).NotEmpty();
        RuleFor(command => command.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(10 * 1024 * 1024);
        RuleFor(command => command.ContentType)
            .Must(contentType => SupportedContentTypes.Contains(contentType.ToLowerInvariant()))
            .WithMessage("La factura debe ser PDF, JPEG o PNG.");
        RuleFor(command => command.FileStream).NotNull();
    }
}
