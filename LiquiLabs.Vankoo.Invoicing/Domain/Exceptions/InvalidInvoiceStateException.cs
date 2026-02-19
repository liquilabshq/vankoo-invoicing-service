using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

/// <summary>
/// Se lanza cuando se intenta ejecutar una operación en un estado incorrecto.
/// 
/// Ejemplo:
/// - Intentar registrar OCR cuando status != OCR_PROCESSING
/// - Intentar aprobar cuando status != SUNAT_VALIDATED
/// </summary>
public class InvalidInvoiceStateException : InvoiceDomainException
{
    public InvoiceStatus CurrentStatus { get; }
    public InvoiceStatus? ExpectedStatus { get; }

    public InvalidInvoiceStateException(string message)
        : base(message)
    {
    }

    public InvalidInvoiceStateException(
        InvoiceStatus currentStatus,
        InvoiceStatus expectedStatus,
        string operation)
        : base($"Cannot {operation} when invoice is in status '{currentStatus}'. " +
               $"Required status: '{expectedStatus}'")
    {
        CurrentStatus = currentStatus;
        ExpectedStatus = expectedStatus;
    }

    public InvalidInvoiceStateException(
        InvoiceStatus currentStatus,
        string operation)
        : base($"Cannot {operation} when invoice is in status '{currentStatus}'")
    {
        CurrentStatus = currentStatus;
    }
}