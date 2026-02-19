namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

/// <summary>
/// Excepción base para todo el dominio de Invoice.
/// Todas las excepciones del dominio heredan de esta.
/// </summary>
public class InvoiceDomainException : Exception
{
    public InvoiceDomainException(string message)
        : base(message)
    {
    }

    public InvoiceDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}