using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public class MinioStorageService : IStorageService
{
    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Pendiente: implementar la descarga de archivos desde MinIO/S3.");
    }
    
}