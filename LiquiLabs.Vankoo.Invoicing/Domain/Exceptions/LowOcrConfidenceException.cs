namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
/// <summary>
/// Se lanza cuando el OCR tiene una confianza menor al mínimo aceptable (80%).
/// 
/// ¿Cuándo ocurre?
/// - PDF de mala calidad (borroso, escaneado mal)
/// - Imagen con poca resolución
/// - Factura con formato no estándar
/// </summary>

public class LowOcrConfidenceException : InvoiceDomainException
{
    public float Confidence { get; }
    public float MinimumRequired { get; }

    public LowOcrConfidenceException(float confidence, float minimumRequired = 0.80f)
        : base($"OCR confidence {confidence:P0} is below the required minimum of {minimumRequired:P0}")
    {
        Confidence = confidence;
        MinimumRequired = minimumRequired;
    }
}