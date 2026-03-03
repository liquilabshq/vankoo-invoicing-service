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
    //private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public MinioStorageService(IOptions<MinioSettings> options)
    {
        //_s3Client = s3Client;
        _bucketName = options.Value.BucketName;
    }
    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken cancellationToken = default)
    {
        var fileStream = File.OpenRead(@"C:\Users\DANIEL\Downloads\factura-oficial.pdf");
        return Task.FromResult<Stream>(fileStream);
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        throw new NotImplementedException("S3/MinIO deshabilitado temporalmente.");}

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var fileStream = File.OpenRead(@"C:\Users\DANIEL\Downloads\factura-oficial.pdf");
        return Task.FromResult<Stream>(fileStream);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        throw new NotImplementedException("S3/MinIO deshabilitado temporalmente.");}

}