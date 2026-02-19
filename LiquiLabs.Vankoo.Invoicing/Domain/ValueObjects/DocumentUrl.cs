namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
public sealed record DocumentUrl
{
    public string OriginalUrl { get; init; }
    public string StoragePath { get; init; }


    private DocumentUrl(string originalUrl, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            throw new ArgumentException("Original URL cannot be empty", nameof(originalUrl));

        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));

        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid URL format", nameof(originalUrl));

        OriginalUrl = originalUrl;
        StoragePath = storagePath;
    }

    public static DocumentUrl Create(string originalUrl, string storagePath)
        => new(originalUrl, storagePath);

    public string GetFileName() => Path.GetFileName(StoragePath);
    public string GetExtension() => Path.GetExtension(StoragePath).ToLowerInvariant();
    public bool IsPdf() => GetExtension() == ".pdf";
    public bool IsImage() => GetExtension() is ".png" or ".jpg" or ".jpeg";

    public override string ToString() => OriginalUrl;
}