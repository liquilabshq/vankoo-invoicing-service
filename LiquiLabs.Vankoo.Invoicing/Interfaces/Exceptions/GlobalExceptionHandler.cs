using LiquiLabs.Vankoo.Invoicing.Application.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error capturado por Global Handler: {Message}", exception.Message);

        var statusCode = exception switch
        {
            InvoiceNotFoundException => StatusCodes.Status404NotFound,
            InvalidRucException => StatusCodes.Status400BadRequest,
            InvalidInvoiceStateException => StatusCodes.Status400BadRequest,
            InvoiceDomainException => StatusCodes.Status400BadRequest,
            
            OcrInvalidDocumentException => StatusCodes.Status422UnprocessableEntity,
            OcrOperationNotFoundException => StatusCodes.Status404NotFound,
            
            OcrProcessingException ex when ex.ErrorCode == OcrErrorCode.RateLimitExceeded => StatusCodes.Status429TooManyRequests,
            OcrProcessingException ex when ex.IsTransient => StatusCodes.Status503ServiceUnavailable,
            OcrProcessingException => StatusCodes.Status502BadGateway,

            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is OcrProcessingException ocrEx)
        {
            problemDetails.Extensions["ocrErrorCode"] = ocrEx.ErrorCode.ToString();
            problemDetails.Extensions["isTransient"] = ocrEx.IsTransient;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true; 
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Error de validación",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status422UnprocessableEntity => "El archivo proporcionado no es válido",
        StatusCodes.Status429TooManyRequests => "Demasiadas peticiones al servicio",
        StatusCodes.Status502BadGateway => "Fallo en el servicio externo",
        StatusCodes.Status503ServiceUnavailable => "Servicio temporalmente no disponible",
        _ => "Error interno del servidor"
    };
}