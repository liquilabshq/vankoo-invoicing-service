using System.Text.Json;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public static class AuctionLifecycleMessageParser
{
    public const string AuctionPublishedEventType = "AuctionPublished";
    public const string EventTypeHeader = "eventType";

    private const int SupportedSchemaVersion = 1;

    /// <summary>
    /// Devuelve el invoiceId si el mensaje es un AuctionPublished, o null si es otro evento
    /// del ciclo de vida de la subasta (que Invoicing no consume). Lanza si el mensaje es inválido.
    /// </summary>
    public static string? TryGetPublishedInvoiceId(string? eventTypeHeader, string body)
    {
        if (!string.IsNullOrWhiteSpace(eventTypeHeader) && eventTypeHeader != AuctionPublishedEventType)
            return null;

        using var document = ParseObject(body);
        var root = document.RootElement;

        if (string.IsNullOrWhiteSpace(eventTypeHeader))
        {
            var bodyEventType = ReadString(root, "eventType")
                                ?? throw new InvalidAuctionLifecycleMessageException(
                                    "El mensaje no trae eventType en la cabecera ni en el cuerpo.");
            if (bodyEventType != AuctionPublishedEventType)
                return null;
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaVersion) ||
            !schemaVersion.TryGetInt32(out var version) ||
            version != SupportedSchemaVersion)
        {
            throw new InvalidAuctionLifecycleMessageException(
                $"schemaVersion no soportado; se esperaba {SupportedSchemaVersion}.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidAuctionLifecycleMessageException("AuctionPublished no trae el objeto data.");

        var invoiceId = ReadString(data, "invoiceId");
        if (!Guid.TryParse(invoiceId, out _))
            throw new InvalidAuctionLifecycleMessageException("data.invoiceId no es un UUID válido.");

        return invoiceId;
    }

    private static JsonDocument ParseObject(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidAuctionLifecycleMessageException("El mensaje no tiene cuerpo.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new InvalidAuctionLifecycleMessageException("El cuerpo del mensaje no es JSON válido.", ex);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidAuctionLifecycleMessageException("El cuerpo del mensaje no es un objeto JSON.");
        }

        return document;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

public sealed class InvalidAuctionLifecycleMessageException : Exception
{
    public InvalidAuctionLifecycleMessageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
