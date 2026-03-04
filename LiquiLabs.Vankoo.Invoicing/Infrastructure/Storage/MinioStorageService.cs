using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public MinioStorageService(IAmazonS3 s3Client, IOptions<MinioSettings> options)
    {
        _s3Client = s3Client;
        _bucketName = options.Value.BucketName;
    }
    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken ct = default)
        => DownloadAsync(fileKey.Value, ct);

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        var response = await _s3Client.GetObjectAsync(request, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, ct);
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
        if (!exists)
            await _s3Client.PutBucketAsync(_bucketName, ct);
    }
}