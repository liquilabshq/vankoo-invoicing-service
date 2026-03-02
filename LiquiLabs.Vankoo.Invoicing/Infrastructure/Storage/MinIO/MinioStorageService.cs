using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.MinIO.Exceptions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.MinIO;

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public MinioStorageService(IAmazonS3 s3Client, IOptions<MinioSettings> options)
    {
        _s3Client = s3Client;
        _bucketName = options.Value.BucketName;
    }
    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Pendiente: implementar la descarga de archivos desde MinIO/S3.");
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        try
        {
            await EnsureBucketExistsAsync(ct);

            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType
            }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            throw new FileUploadException(key, ex);
        }
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            }, ct);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileDownloadException(key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageUnavailableException(ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageUnavailableException(ex);
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
                await _s3Client.PutBucketAsync(_bucketName, ct);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageUnavailableException(ex);
        }
    }
}
