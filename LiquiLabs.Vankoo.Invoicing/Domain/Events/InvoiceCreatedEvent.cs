namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

//PUEDE QUE NO CUENTE

/// <summary>
/// Se dispara cuando un MYPE sube una nueva factura.
/// 
/// ¿Quién lo consume?
/// - Notification Service: Envía email/push de confirmación al MYPE
/// - (Opcional) Audit Service: Registra la acción
/// 
/// ¿Cuándo ocurre?
/// Cuando Invoice.Create() es llamado
/// </summary>
public sealed record InvoiceCreatedEvent
{
    public string InvoiceId { get; init; }

    public string MypeId { get; init; }

    public string FileUrl { get; init; }

    public DateTime OccurredOn { get; init; }

    public InvoiceCreatedEvent(
        string invoiceId,
        string mypeId,
        string fileUrl,
        DateTime occurredOn)
    {
        InvoiceId = invoiceId ?? throw new ArgumentNullException(nameof(invoiceId));
        MypeId = mypeId ?? throw new ArgumentNullException(nameof(mypeId));
        FileUrl = fileUrl ?? throw new ArgumentNullException(nameof(fileUrl));
        OccurredOn = occurredOn;
    }
}