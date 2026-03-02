using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Application.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Application.Exceptions;

public class OcrTimeoutException : OcrProcessingException
{
    public OcrTimeoutException(string message, Exception? innerException = null)
        : base(message, OcrErrorCode.Timeout, isTransient: true, innerException)
    {
    }
}
