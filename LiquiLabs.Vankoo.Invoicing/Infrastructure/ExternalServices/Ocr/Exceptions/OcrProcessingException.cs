using LiquiLabs.Vankoo.Invoicing.Shared.Domain;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Exceptions;

public sealed class OcrProcessingException : InfrastructureException
{
    public string? FileUrl { get; }
    public string? OperationId { get; }

    public OcrProcessingException(string message, Exception? innerException = null)
        : base(ErrorCodes.InternalError, message, innerException) { }

    public OcrProcessingException(string message, string? fileUrl, Exception? innerException = null)
        : base(ErrorCodes.InternalError, message, innerException)
    {
        FileUrl = fileUrl;
    }

    public OcrProcessingException(string message, string? fileUrl, string? operationId, Exception? innerException = null)
        : base(ErrorCodes.InternalError, message, innerException)
    {
        FileUrl = fileUrl;
        OperationId = operationId;
    }
}
