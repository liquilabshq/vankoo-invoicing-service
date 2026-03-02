using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.MinIO.Exceptions;

public sealed class StorageUnavailableException : StorageException
{
    public StorageUnavailableException(Exception? innerException = null)
        : base("STORAGE_UNAVAILABLE", "El servicio de almacenamiento no está disponible.", innerException) { }
}
