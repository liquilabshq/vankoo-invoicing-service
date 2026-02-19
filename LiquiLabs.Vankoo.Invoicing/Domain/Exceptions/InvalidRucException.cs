namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
/// <summary>
/// Se lanza cuando un RUC no cumple con el formato peruano válido.
/// 
/// Reglas de RUC en Perú:
/// - 11 dígitos numéricos
/// - Primeros 2 dígitos: 10 (persona natural), 15, 20 (empresa)
/// </summary>
public class InvalidRucException : InvoiceDomainException
{
    public string InvalidRuc { get; }

    public InvalidRucException(string invalidRuc)
        : base($"RUC '{invalidRuc}' is not valid. Must be 11 digits starting with 10, 15, or 20")
    {
        InvalidRuc = invalidRuc;
    }

    public InvalidRucException(string invalidRuc, string specificReason)
        : base($"RUC '{invalidRuc}' is invalid: {specificReason}")
    {
        InvalidRuc = invalidRuc;
    }
}