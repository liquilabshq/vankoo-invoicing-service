using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Interfaces;

public interface IStorageService
{
    Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken cancellationToken = default);
}