using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;

public sealed class Invoice
{
    public InvoiceId Id { get; private set; }
    public MypeId MypeId { get; private set; }
    public PayerData? PayerData { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public InvoiceDocument Document { get; private set; }
    public Money? TotalAmount { get; private set; }
    public OcrOperationId? OcrOperationId { get; private set; }
    public InvoiceMetadata? Metadata { get; private set; }
    public SunatValidation? SunatValidation { get; private set; }
    public IReadOnlyList<InvoiceLineItem> Items => _items.AsReadOnly();
    public RejectionReason? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<InvoiceLineItem> _items = [];
    

    // ========== CONSTRUCTOR PRIVADO (para MongoDB) ==========
    private Invoice() { }
    
    private Invoice(InvoiceId id, MypeId mypeId, InvoiceDocument  document)
    {
        Id = id;
        MypeId = mypeId;
        Document = document;
        Status = InvoiceStatus.UPLOADED;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // ========== FACTORY METHOD ==========
    public static Invoice Create(MypeId mypeId, InvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(mypeId);
        ArgumentNullException.ThrowIfNull(document);

        return new Invoice(InvoiceId.NewId(), mypeId, document);
    }
    
    public void StartOcrProcessing()
    {
        if (Status != InvoiceStatus.UPLOADED)
            throw new InvalidOperationException("Only invoices in UPLOADED status can start OCR processing");

        Status = InvoiceStatus.OCR_PROCESSING;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void SetOcrOperationId(OcrOperationId operationId)
    {
        if (Status != InvoiceStatus.OCR_PROCESSING)
            throw new InvalidOperationException("OCR Operation ID can only be set for invoices in OCR_PROCESSING status");

        OcrOperationId = operationId;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Reject(RejectionReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        
        if (!CanBeRejected())
            throw new InvalidInvoiceStateException(Status, "reject");

        RejectionReason = reason;
        Status = InvoiceStatus.REJECTED;
        UpdatedAt = DateTime.UtcNow;
    }
    

    public void RegisterOcrResults(OcrExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Validar que el estado actual permita registrar resultados OCR
        if (Status != InvoiceStatus.OCR_PROCESSING)
        {
            throw new InvalidInvoiceStateException(Status,"Register OCR results");
        }
        
        // Validar que el resultado OCR tenga una confianza aceptable (al menos 80%)
        if (!result.Metadata.HasAcceptableConfidence(0.80f))
        {
            throw new LowOcrConfidenceException(result.Metadata.GetOcrConfidencePercentage());
        }
        
        // Validar consistencia entre el total extraído y la suma de los items
        // if (!result.HasConsistentTotal())
        // {
        //     throw new InvalidValueException("INCONSISTENT_INVOICE_TOTAL",
        //         "The sum of the invoice line items does not match the total amount extracted.");
        // }
        
        // Asignar los datos extraídos al invoice
        PayerData = result.PayerData;
        TotalAmount = result.TotalAmount;
        Metadata = result.Metadata;
        
        // Limpiar cualquier resultado OCR previo (en caso de reintentos)
        _items.Clear();
        _items.AddRange(result.Items);
        
        // Actualizar el estado a DATA_EXTRACTED
        Status = InvoiceStatus.DATA_EXTRACTED;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureReadyForOcrProcessedEvent()
    {
        if (Status != InvoiceStatus.DATA_EXTRACTED)
            throw new InvalidInvoiceStateException(Status, "publish OCR processed integration event");

        if (PayerData is null)
            throw new IncompleteOcrDataException(Id.Value, "PayerData");

        if (PayerData.Ruc is null || string.IsNullOrWhiteSpace(PayerData.Ruc.Value))
            throw new IncompleteOcrDataException(Id.Value, "PayerRuc");

        if (string.IsNullOrWhiteSpace(PayerData.GetDisplayName()))
            throw new IncompleteOcrDataException(Id.Value, "PayerName");

        if (TotalAmount is null)
            throw new IncompleteOcrDataException(Id.Value, "TotalAmount");

        if (TotalAmount.Amount <= 0)
            throw new IncompleteOcrDataException(Id.Value, "TotalAmount(>0)");

        if (Metadata is null)
            throw new IncompleteOcrDataException(Id.Value, "Metadata");

        if (Metadata.DueDate == default)
            throw new IncompleteOcrDataException(Id.Value, "DueDate");
    }
    
    private bool CanBeRejected()
        => Status is
            InvoiceStatus.UPLOADED or
            InvoiceStatus.OCR_PROCESSING or
            InvoiceStatus.DATA_EXTRACTED or
            InvoiceStatus.SUNAT_VALIDATING or
            InvoiceStatus.SUNAT_VALIDATED;
}
