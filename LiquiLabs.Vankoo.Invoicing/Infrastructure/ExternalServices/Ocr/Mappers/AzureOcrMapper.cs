using Azure.AI.FormRecognizer.DocumentAnalysis;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Mappers;

public class AzureOcrMapper
{
    private readonly ILogger<AzureOcrMapper> _logger;

    public AzureOcrMapper(ILogger<AzureOcrMapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public OcrExtractionResult MapToOcrExtractionResult(AnalyzeResult azureResult)
    {
        var document = azureResult.Documents.FirstOrDefault();
        if (document == null)
        {
            throw new InvalidOperationException("No document found in Azure result");
        }
        
        // Extraer campos del documento
        var fields = document.Fields;

        // Extraer datos de los campos de Azure
        var invoiceId = GetStringField(fields, "InvoiceId") ?? "UNKNOWN";
        var (series, number) = ParseInvoiceNumber(invoiceId);

        var issueDate = GetDateField(fields, "InvoiceDate") ?? DateTime.UtcNow;
        var dueDate = GetDateField(fields, "DueDate") ?? issueDate.AddDays(30);

        var payerRuc = GetStringField(fields, "CustomerTaxId") ?? "00000000000";
        var payerName = GetStringField(fields, "CustomerName") ?? "Unknown Customer";
        var payerAddress = GetStringField(fields, "CustomerAddress");

        var totalAmount = GetDecimalField(fields, "InvoiceTotal") ?? 0m;
        var currencyCode = GetStringField(fields, "CurrencyCode") ?? "PEN";
        var currency = ParseCurrency(currencyCode);

        // Calcular confianza promedio
        var confidence = CalculateAverageConfidence(fields);

        // Extraer items
        var items = ExtractLineItems(fields, currency);

        // Crear PayerData
        var payerData = PayerData.Create(
            RucNumber.Of(payerRuc),
            payerName,
            tradeName: null,
            address: payerAddress);

        // Crear InvoiceMetadata
        var metadata = InvoiceMetadata.Create(
            series,
            number,
            issueDate,
            dueDate,
            currency,
            confidence);

        // Crear Money
        var money = Money.Of(totalAmount, currency);

        _logger.LogInformation(
            "Mapped Azure result: Invoice {InvoiceNumber}, Amount {Amount}, Confidence {Confidence:P0}",
            $"{series}-{number}",
            totalAmount,
            confidence);

        return new OcrExtractionResult(payerData, metadata, money, items);
    }

    private List<InvoiceLineItem> ExtractLineItems(
        IReadOnlyDictionary<string, DocumentField> fields,
        Currency currency)
    {
        var items = new List<InvoiceLineItem>();

        if (!fields.TryGetValue("Items", out var itemsField) || itemsField.FieldType != DocumentFieldType.List)
        {
            _logger.LogWarning("No items field found in Azure result");
            return items;
        }

        foreach (var itemField in itemsField.Value.AsList())
        {
            if (itemField.FieldType != DocumentFieldType.Dictionary)
                continue;

            var itemFields = itemField.Value.AsDictionary();

            var description = GetStringField(itemFields, "Description") ?? "Unknown Item";
            var quantity = GetDecimalField(itemFields, "Quantity") ?? 1;
            var unitPrice = GetDecimalField(itemFields, "UnitPrice") ?? 0m;
            var amount = GetDecimalField(itemFields, "Amount") ?? unitPrice * quantity;

            try
            {
                var lineItem = InvoiceLineItem.CreateFromOcr(
                    description,
                    quantity,
                    Money.Of(unitPrice, currency),
                    Money.Of(amount, currency));

                items.Add(lineItem);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create line item: {Description}", description);
            }
        }

        return items;
    }

    private static (string Series, string Number) ParseInvoiceNumber(string invoiceId)
    {
        // Formato esperado: "F001-00012345" o "B001-123"
        var parts = invoiceId.Split('-');
        if (parts.Length == 2)
        {
            return (parts[0].Trim(), parts[1].Trim());
        }

        // Si no tiene el formato esperado, usar todo como número
        return ("F001", invoiceId);
    }

    private static Currency ParseCurrency(string currencyCode)
    {
        return currencyCode.ToUpperInvariant() switch
        {
            "PEN" or "S/" or "SOLES" => Currency.PEN,
            "USD" or "$" or "DOLARES" => Currency.USD,
            _ => Currency.PEN // Default
        };
    }

    private static string? GetStringField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) && 
            field.FieldType == DocumentFieldType.String)
        {
            return field.Value.AsString();
        }
        return null;
    }

    private static decimal? GetDecimalField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field))
        {
            if (field.FieldType == DocumentFieldType.Double)
                return (decimal)field.Value.AsDouble();
            
            if (field.FieldType == DocumentFieldType.Int64)
                return field.Value.AsInt64();
        }
        return null;
    }

    private static DateTime? GetDateField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) && 
            field.FieldType == DocumentFieldType.Date)
        {
            var d = field.Value.AsDate();
            return new DateTime(d.Year, d.Month, d.Day);
        }
        return null;
    }

    private static float CalculateAverageConfidence(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        var confidences = fields.Values
            .Where(f => f.Confidence.HasValue)
            .Select(f => f.Confidence!.Value)
            .ToList();

        return confidences.Any() ? confidences.Average() : 0.5f;
    }
}