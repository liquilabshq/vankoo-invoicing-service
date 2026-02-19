namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record OcrExtractionResult
{
    public PayerData PayerData { get; init; }
    public InvoiceMetadata Metadata { get; init; }
    public Money TotalAmount { get; init; }
    public IReadOnlyList<InvoiceLineItem> Items { get; init; }

    public OcrExtractionResult(
        PayerData payerData,
        InvoiceMetadata metadata,
        Money totalAmount,
        IReadOnlyList<InvoiceLineItem> items)
    {
        PayerData = payerData ?? throw new ArgumentNullException(nameof(payerData));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        TotalAmount = totalAmount ?? throw new ArgumentNullException(nameof(totalAmount));
        Items = items ?? throw new ArgumentNullException(nameof(items));

        if (!items.Any())
            throw new ArgumentException("Items cannot be empty");
    }

    public bool HasConsistentTotal()
    {
        var itemsTotal = Items.Sum(i => i.Subtotal.Amount);
        return Math.Abs(TotalAmount.Amount - itemsTotal) <= 0.02m;
    }
}