namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
/// <summary>
/// Se lanza cuando no se encuentra una factura por su ID.
/// Útil en queries.
/// </summary>
public class InvoiceNotFoundException : InvoiceDomainException
{
    public string InvoiceId { get; }

    public InvoiceNotFoundException(string invoiceId)
        : base($"Invoice with ID '{invoiceId}' was not found")
    {
        InvoiceId = invoiceId;
    }
}