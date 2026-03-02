using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.MinIO.Exceptions;

public sealed class FileDownloadException : StorageException
{
    public string Key { get; }

    public FileDownloadException(string key, Exception? innerException = null)
        : base("STORAGE_DOWNLOAD_FAILED", $"No se encontró el archivo con clave '{key}'.", innerException)
    {
        Key = key;
    }
}
