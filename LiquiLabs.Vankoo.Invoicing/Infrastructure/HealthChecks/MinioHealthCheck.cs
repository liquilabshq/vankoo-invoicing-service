using Amazon.S3;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.HealthChecks;

/// <summary>
/// Verifica la disponibilidad de MinIO intentando localizar el bucket configurado.
/// Reutiliza el IAmazonS3 singleton ya configurado con ForcePathStyle=true,
/// garantizando compatibilidad con MinIO sin duplicar configuración.
/// </summary>
public class MinioHealthCheck : IHealthCheck
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public MinioHealthCheck(IAmazonS3 s3Client, IOptions<MinioSettings> settings)
    {
        _s3Client = s3Client;
        _bucketName = settings.Value.BucketName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetBucketLocationAsync(_bucketName, cancellationToken);
            return HealthCheckResult.Healthy("MinIO accesible.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: $"MinIO no disponible: {ex.Message}",
                exception: ex);
        }
    }
}
