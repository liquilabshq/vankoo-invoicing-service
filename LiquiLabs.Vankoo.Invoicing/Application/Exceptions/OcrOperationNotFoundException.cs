using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Application.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Application.Exceptions;

public class OcrOperationNotFoundException : OcrProcessingException
{
    public OcrOperationNotFoundException(string message, Exception? innerException = null)
        : base(message, OcrErrorCode.OperationNotFound, isTransient: false, innerException)
    {
    }
}
