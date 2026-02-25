namespace LiquiLabs.Vankoo.Invoicing.Application.Exceptions;

public class OcrProcessingException : Exception
{
    public string? FileUrl { get; }
    public string? OperationId { get; }

    public OcrProcessingException(string message) 
        : base(message) 
    {
    }

    public OcrProcessingException(string message, Exception innerException)
        : base(message, innerException) 
    {
    }

    public OcrProcessingException(string message, string? fileUrl, Exception? innerException = null)
        : base(message, innerException) 
    {
        FileUrl = fileUrl;
    }

    public OcrProcessingException(
        string message, 
        string? fileUrl, 
        string? operationId, 
        Exception? innerException = null)
        : base(message, innerException) 
    {
        FileUrl = fileUrl;
        OperationId = operationId;
    }
}