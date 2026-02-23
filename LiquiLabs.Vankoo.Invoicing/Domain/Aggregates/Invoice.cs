using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MongoDB.Bson.Serialization.Attributes;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;

public sealed class Invoice
{
    [BsonId]
    public InvoiceId Id { get; private set; }
    public MypeId MypeId { get; private set; }
    public PayerData? PayerData { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public InvoiceDocument Document { get; private set; }
    public Money? TotalAmount { get; private set; }
    public string? OcrOperationId { get; private set; }
    public InvoiceMetadata? Metadata { get; private set; }
    public SunatValidation? SunatValidation { get; private set; }
    public IReadOnlyList<InvoiceLineItem> Items => _items.AsReadOnly();
    public RejectionReason? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<InvoiceLineItem> _items = [];

    // ========== CONSTRUCTOR PRIVADO (para MongoDB) ==========
    private Invoice() { }

    // ========== FACTORY METHOD ==========
    public static Invoice Create(MypeId mypeId, InvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(mypeId);
        ArgumentNullException.ThrowIfNull(document);

        return new Invoice
        {
            Id = InvoiceId.NewId(),
            MypeId = mypeId,
            Document = document,
            Status = InvoiceStatus.UPLOADED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
