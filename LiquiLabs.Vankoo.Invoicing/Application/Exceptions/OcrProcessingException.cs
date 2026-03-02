namespace LiquiLabs.Vankoo.Invoicing.Application.Exceptions;

public class OcrProcessingException : Exception
{
    public OcrErrorCode ErrorCode { get; }
    public bool IsTransient { get; }

    public OcrProcessingException(string message)
        : base(message)
    {
        ErrorCode = OcrErrorCode.Unknown;
        IsTransient = false;
    }

    public OcrProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = OcrErrorCode.Unknown;
        IsTransient = false;
    }

    // ¡Cambiado de protected a public!
    public OcrProcessingException(string message, OcrErrorCode errorCode, bool isTransient)
        : base(message)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    // ¡Cambiado de protected a public!
    public OcrProcessingException(string message, OcrErrorCode errorCode, bool isTransient, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }
}