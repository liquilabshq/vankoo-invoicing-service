using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Application.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Application.Exceptions;

public class OcrServiceUnavailableException : OcrProcessingException
{
    public OcrServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, OcrErrorCode.RateLimitExceeded, isTransient: true, innerException)
    {
    }
}
