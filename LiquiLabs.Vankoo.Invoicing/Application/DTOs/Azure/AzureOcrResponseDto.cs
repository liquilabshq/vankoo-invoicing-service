namespace LiquiLabs.Vankoo.Invoicing.Application.DTOs.Azure;


// DTO para mapear la respuesta de Azure Form Recognizer
public class AzureOcrResponseDto
{
    public string Status { get; set; } = string.Empty; // "succeeded", "failed", "running"
    public string? OperationId { get; set; }
    public AzureAnalyzeResultDto? AnalyzeResult { get; set; }
    public string? Error { get; set; }
}

public class AzureAnalyzeResultDto
{
    public List<AzureDocumentDto> Documents { get; set; } = new();
}

public class AzureDocumentDto
{
    public string DocType { get; set; } = string.Empty;
    public Dictionary<string, AzureFieldDto> Fields { get; set; } = new();
}

public class AzureFieldDto
{
    public string Type { get; set; } = string.Empty; // "string", "number", "date", etc.
    public string? ValueString { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTime? ValueDate { get; set; }
    public float Confidence { get; set; }
    public List<AzureFieldDto>? ValueArray { get; set; } // Para items de factura
}