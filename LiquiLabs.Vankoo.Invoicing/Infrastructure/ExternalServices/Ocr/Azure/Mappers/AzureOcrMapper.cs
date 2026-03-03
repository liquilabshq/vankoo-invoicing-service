using System.Globalization;
using System.Text.RegularExpressions;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Application.Exceptions;
using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Mappers;

public class AzureOcrMapper
{
    private readonly ILogger<AzureOcrMapper> _logger;

    private static readonly Regex PeruvianDatePattern =
        new(@"\b(\d{2})/(\d{2})/(\d{4})\b", RegexOptions.Compiled);

    private static readonly Regex InvoiceNumberPattern =
        new(@"\b[EFBR]\d{3}-\d+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AzureOcrMapper(ILogger<AzureOcrMapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public OcrExtractionResult MapToOcrExtractionResult(AnalyzeResult azureResult)
    {
        var document = azureResult.Documents.FirstOrDefault();
        if (document == null)
            throw new OcrProcessingException(
                "Azure no encontró ningún documento en el archivo.",
                OcrErrorCode.InvalidDocument,
                isTransient: false);

        var fields = document.Fields;

        LogRawFields(fields);

        // ── Número de factura ──────────────────────────────────────────────
        var invoiceId = GetStringField(fields, "InvoiceId")
                     ?? GetStringField(fields, "InvoiceNumber")
                     ?? ExtractFromContent(azureResult, InvoiceNumberPattern)
                     ?? throw new OcrProcessingException(
                            "No se pudo extraer el número de factura.",
                            OcrErrorCode.InvalidDocument, isTransient: false);

        var (series, number) = ParseInvoiceNumber(invoiceId);

        // ── Fechas ─────────────────────────────────────────────────────────
        var issueDate = GetDateField(fields, "InvoiceDate")
                     ?? ParseDateFromContent(azureResult, "Fecha de Emisión")
                     ?? ParseDateFromContent(azureResult, "Fecha de Emision")
                     ?? ParseDateFromContent(azureResult, "Emisión")
                     ?? ParseDateFromContent(azureResult, "Emision")
                     ?? throw new OcrProcessingException(
                            "No se pudo extraer la fecha de emisión de la factura.",
                            OcrErrorCode.InvalidDocument, isTransient: false);

        var dueDate = GetDateField(fields, "DueDate")
                   ?? ParseDateFromContent(azureResult, "Fecha de Vencimiento")
                   ?? ParseDateFromContent(azureResult, "Vencimiento")
                   ?? throw new OcrProcessingException(
                          "No se pudo extraer la fecha de vencimiento de la factura.",
                          OcrErrorCode.InvalidDocument, isTransient: false);

        // ── Cliente ────────────────────────────────────────────────────────
        var payerRuc = GetStringField(fields, "CustomerTaxId")
                    ?? throw new OcrProcessingException(
                           "No se pudo extraer el RUC del cliente.",
                           OcrErrorCode.InvalidDocument, isTransient: false);

        var payerName = SanitizeText(
                            GetStringField(fields, "CustomerName") 
                            ?? GetStringField(fields, "CustomerAddressRecipient")) 
                        ?? throw new OcrProcessingException(
                            "No se pudo extraer el nombre del cliente.",
                            OcrErrorCode.InvalidDocument, isTransient: false);
        
        var payerAddress = SanitizeText(
            GetAddressField(fields, "CustomerAddress")
            ?? GetStringField(fields, "CustomerAddress"));
        
        // ── Montos ─────────────────────────────────────────────────────────
        var totalAmount = GetDecimalField(fields, "InvoiceTotal")
                       ?? GetDecimalField(fields, "AmountDue")
                       ?? throw new OcrProcessingException(
                              "No se pudo extraer el monto total de la factura.",
                              OcrErrorCode.InvalidDocument, isTransient: false);

        // ── Moneda ─────────────────────────────────────────────────────────
        var currencyCode = GetStringField(fields, "CurrencyCode")
                        ?? InferCurrencyFromContent(azureResult);
        var currency = ParseCurrency(currencyCode);

        // ── Confianza ──────────────────────────────────────────────────────
        var confidence = CalculateAverageConfidence(fields);

        // ── Items ──────────────────────────────────────────────────────────
        var items = ExtractLineItems(fields, currency);

        // ── Construir Value Objects ────────────────────────────────────────
        var payerData = PayerData.Create(
            RucNumber.Of(CleanRuc(payerRuc)),
            payerName,
            tradeName: null,
            address: payerAddress);

        var metadata = InvoiceMetadata.Create(
            series,
            number,
            issueDate,
            dueDate,
            currency,
            confidence);

        var money = Money.Of(totalAmount, currency);

        _logger.LogInformation(
            "Mapped invoice {Series}-{Number} | Amount: {Amount} {Currency} | IssueDate: {IssueDate:d} | DueDate: {DueDate:d} | Confidence: {Confidence:P0}",
            series, number, totalAmount, currency, issueDate, dueDate, confidence);

        return new OcrExtractionResult(payerData, metadata, money, items);
    }

    // ── Line Items ─────────────────────────────────────────────────────────

    private List<InvoiceLineItem> ExtractLineItems(
        IReadOnlyDictionary<string, DocumentField> fields,
        Currency currency)
    {
        var items = new List<InvoiceLineItem>();

        if (!fields.TryGetValue("Items", out var itemsField) ||
            itemsField.FieldType != DocumentFieldType.List)
        {
            _logger.LogWarning("No Items field found in Azure result.");
            return items;
        }

        foreach (var itemField in itemsField.Value.AsList())
        {
            if (itemField.FieldType != DocumentFieldType.Dictionary)
                continue;

            var itemFields = itemField.Value.AsDictionary();

            var description = GetStringField(itemFields, "Description") ?? "Unknown Item";
            var quantity    = GetDecimalField(itemFields, "Quantity")    ?? 1m;
            var unitPrice   = GetDecimalField(itemFields, "UnitPrice")   ?? 0m;
            var amount      = GetDecimalField(itemFields, "Amount")      ?? unitPrice * quantity;

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

    // ── Field Extractors ───────────────────────────────────────────────────

    private static string? GetStringField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.String)
            return field.Value.AsString();

        return null;
    }

    /// Maneja campos de tipo Address — Azure los devuelve como objeto, no string
    private static string? GetAddressField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return null;

        return field.FieldType switch
        {
            DocumentFieldType.String  => field.Value.AsString(),
            DocumentFieldType.Address => field.Content,
            _                         => null
        };
    }

    /// Maneja Double, Int64 y Currency — facturas peruanas usan Currency type
    private static decimal? GetDecimalField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return null;

        return field.FieldType switch
        {
            DocumentFieldType.Double   => (decimal)field.Value.AsDouble(),
            DocumentFieldType.Int64    => field.Value.AsInt64(),
            DocumentFieldType.Currency => (decimal)field.Value.AsCurrency().Amount,
            _                          => null
        };
    }

    private static DateTime? GetDateField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.Date)
        {
            var d = field.Value.AsDate();
            return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        }
        return null;
    }

    // ── Fallbacks sobre texto crudo ────────────────────────────────────────

    /// Busca una fecha DD/MM/YYYY en la línea que contiene la keyword,
    /// o en la línea inmediatamente siguiente (Azure a veces las separa)
    private static DateTime? ParseDateFromContent(AnalyzeResult result, string nearKeyword)
    {
        foreach (var page in result.Pages)
        {
            var lines = page.Lines.ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Content.Contains(nearKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Buscar en la línea actual y la siguiente
                var searchTargets = new List<string> { lines[i].Content };
                if (i + 1 < lines.Count)
                    searchTargets.Add(lines[i + 1].Content);

                foreach (var target in searchTargets)
                {
                    var match = PeruvianDatePattern.Match(target);
                    if (!match.Success) continue;

                    if (DateTime.TryParseExact(
                            match.Value,
                            "dd/MM/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var date))
                        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
                }
            }
        }
        return null;
    }

    /// Extrae el primer match de un patrón del texto crudo de todas las páginas
    private static string? ExtractFromContent(AnalyzeResult result, Regex pattern)
    {
        foreach (var page in result.Pages)
        {
            foreach (var line in page.Lines)
            {
                var match = pattern.Match(line.Content);
                if (match.Success) return match.Value;
            }
        }
        return null;
    }

    /// Infiere la moneda del texto crudo cuando Azure no la detecta como campo
    private static string InferCurrencyFromContent(AnalyzeResult result)
    {
        var content = string.Join(" ", result.Pages
            .SelectMany(p => p.Lines)
            .Select(l => l.Content));

        if (content.Contains("NUEVOS SOLES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("S/.", StringComparison.OrdinalIgnoreCase))
            return "PEN";

        if (content.Contains("DOLARES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("USD", StringComparison.OrdinalIgnoreCase))
            return "USD";

        return "PEN";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static (string Series, string Number) ParseInvoiceNumber(string invoiceId)
    {
        var parts = invoiceId.Split('-');
        return parts.Length == 2
            ? (parts[0].Trim(), parts[1].Trim())
            : ("F001", invoiceId);
    }

    private static Currency ParseCurrency(string currencyCode)
    {
        return currencyCode.ToUpperInvariant() switch
        {
            "PEN" or "S/" or "SOLES" or "NUEVOS SOLES" => Currency.PEN,
            "USD" or "$"  or "DOLARES"                  => Currency.USD,
            _                                            => Currency.PEN
        };
    }

    private static string CleanRuc(string ruc) =>
        new string(ruc.Where(char.IsDigit).ToArray());

    private static float CalculateAverageConfidence(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        var confidences = fields.Values
            .Where(f => f.Confidence.HasValue)
            .Select(f => f.Confidence!.Value)
            .ToList();

        return confidences.Any() ? confidences.Average() : 0.5f;
    }

    private void LogRawFields(IReadOnlyDictionary<string, DocumentField> fields)
    {
        if (!_logger.IsEnabled(LogLevel.Warning)) return;

        _logger.LogWarning("=== AZURE RAW FIELDS ===");
        foreach (var (key, field) in fields)
        {
            _logger.LogWarning(
                "Field: {Key} | Type: {Type} | Value: {Value} | Confidence: {Conf:P0}",
                key, field.FieldType, field.Content, field.Confidence ?? 0);
        }
    }
    
    private static string SanitizeText(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : Regex.Replace(text.Replace("\n", " ").Replace("\r", " "), @"\s{2,}", " ").Trim();
        
    
}