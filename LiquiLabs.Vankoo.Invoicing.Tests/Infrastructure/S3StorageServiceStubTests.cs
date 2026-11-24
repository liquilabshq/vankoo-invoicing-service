using Amazon.Runtime;
using Amazon.S3;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Stubs;
using static LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Stubs.HttpStubServer;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

/// <summary>
/// Pruebas de integración de <see cref="S3StorageService"/> contra un stub HTTP
/// con la API de S3. Se usa el <see cref="AmazonS3Client"/> real, configurado igual
/// que en <c>Program.cs</c> para el proveedor compatible con S3 (ServiceURL +
/// path-style), de modo que se ejercita la serialización del SDK de punta a punta.
/// </summary>
/// <author>LiquiLabs</author>
public sealed class S3StorageServiceStubTests : IAsyncLifetime
{
    private const string Bucket = "vankoo-invoices";
    private const string Key = "invoices/0199a3e4/factura-E001-28.pdf";
    private static readonly byte[] PdfBytes = "%PDF-1.4 factura de prueba"u8.ToArray();

    private HttpStubServer _s3 = null!;

    public async Task InitializeAsync() => _s3 = await HttpStubServer.StartAsync();

    public async Task DisposeAsync() => await _s3.DisposeAsync();

    private S3StorageService CreateService(bool autoCreateBucket = false, string? serviceUrl = null)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl ?? _s3.BaseUrl,
            ForcePathStyle = true,
            // Sin reintentos: cada operación del adaptador es una única solicitud observable.
            MaxErrorRetry = 0
        };
        var client = new AmazonS3Client(new BasicAWSCredentials("test-access", "test-secret"), config);
        var settings = new ObjectStorageRuntimeSettings(
            StorageProvider.Minio, Bucket, "test-access", "test-secret",
            Endpoint: "", Region: "us-east-1", UseSsl: false, AutoCreateBucket: autoCreateBucket);
        return new S3StorageService(client, settings);
    }

    private static StubResponse S3Error(int status, string code, string message) => new(
        status,
        $"""<?xml version="1.0" encoding="UTF-8"?><Error><Code>{code}</Code><Message>{message}</Message><RequestId>stub</RequestId></Error>""",
        "application/xml");

    [Fact]
    public async Task Upload_PutsTheObjectInTheBucketWithItsContentType()
    {
        _s3.WillRespond(new StubResponse(200, Headers: new Dictionary<string, string> { ["ETag"] = "\"abc\"" }));

        await CreateService().UploadAsync(Key, new MemoryStream(PdfBytes), "application/pdf");

        var request = Assert.Single(_s3.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.StartsWith($"/{Bucket}/{Key}", request.PathAndQuery);
        Assert.Equal("application/pdf", request.Header("Content-Type"));
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=test-access/", request.Header("Authorization"));
        Assert.Contains("%PDF-1.4 factura de prueba", request.BodyText);
        Assert.DoesNotContain("test-secret", string.Join(' ', request.Headers.Values));
    }

    [Fact]
    public async Task Upload_CreatesTheBucketFirstWhenItDoesNotExist()
    {
        _s3.WillRespond(S3Error(404, "NoSuchBucket", "The specified bucket does not exist"))
            .WillRespond(new StubResponse(200))
            .WillRespond(new StubResponse(200, Headers: new Dictionary<string, string> { ["ETag"] = "\"abc\"" }));

        await CreateService(autoCreateBucket: true).UploadAsync(Key, new MemoryStream(PdfBytes), "application/pdf");

        var requests = _s3.Requests;
        Assert.Equal(3, requests.Count);
        Assert.Equal(("PUT", $"/{Bucket}"), (requests[1].Method, requests[1].PathAndQuery.Split('?')[0].TrimEnd('/')));
        Assert.Equal("PUT", requests[2].Method);
        Assert.StartsWith($"/{Bucket}/{Key}", requests[2].PathAndQuery);
    }

    [Fact]
    public async Task Upload_DoesNotRecreateAnExistingBucket()
    {
        _s3.WillRespond(new StubResponse(200, "<AccessControlPolicy/>", "application/xml"))
            .WillRespond(new StubResponse(200, Headers: new Dictionary<string, string> { ["ETag"] = "\"abc\"" }));

        await CreateService(autoCreateBucket: true).UploadAsync(Key, new MemoryStream(PdfBytes), "application/pdf");

        Assert.Equal(2, _s3.Requests.Count);
        Assert.StartsWith($"/{Bucket}/{Key}", _s3.Requests[1].PathAndQuery);
    }

    [Fact]
    public async Task Download_ReturnsTheObjectStream()
    {
        _s3.WillRespond(new StubResponse(200, ContentType: "application/pdf", BinaryBody: PdfBytes));

        await using var stream = await CreateService().DownloadAsync(Key);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal(PdfBytes, copy.ToArray());
        var request = Assert.Single(_s3.Requests);
        Assert.Equal("GET", request.Method);
        Assert.StartsWith($"/{Bucket}/{Key}", request.PathAndQuery);
    }

    [Fact]
    public async Task Delete_RemovesTheObject()
    {
        _s3.WillRespond(new StubResponse(204));

        await CreateService().DeleteAsync(Key);

        var request = Assert.Single(_s3.Requests);
        Assert.Equal("DELETE", request.Method);
        Assert.StartsWith($"/{Bucket}/{Key}", request.PathAndQuery);
    }

    [Theory]
    [InlineData(403, "AccessDenied", "Access Denied")]
    [InlineData(404, "NoSuchBucket", "The specified bucket does not exist")]
    [InlineData(500, "InternalError", "We encountered an internal error")]
    [InlineData(503, "SlowDown", "Please reduce your request rate")]
    public async Task Upload_TranslatesS3ErrorsIntoFileUploadException(int status, string code, string message)
    {
        _s3.WillRespond(S3Error(status, code, message));

        var error = await Assert.ThrowsAsync<FileUploadException>(
            () => CreateService().UploadAsync(Key, new MemoryStream(PdfBytes), "application/pdf"));

        var s3Error = Assert.IsAssignableFrom<AmazonS3Exception>(error.InnerException);
        Assert.Equal(code, s3Error.ErrorCode);
        Assert.Equal(status, (int)s3Error.StatusCode);
    }

    [Fact]
    public async Task Download_OfAMissingKeyIsAFileDownloadException()
    {
        _s3.WillRespond(S3Error(404, "NoSuchKey", "The specified key does not exist."));

        var error = await Assert.ThrowsAsync<FileDownloadException>(() => CreateService().DownloadAsync(Key));

        Assert.Equal("NoSuchKey", Assert.IsAssignableFrom<AmazonS3Exception>(error.InnerException).ErrorCode);
    }

    [Fact]
    public async Task Delete_WithoutPermissionIsAFileDeleteException()
    {
        _s3.WillRespond(S3Error(403, "AccessDenied", "Access Denied"));

        await Assert.ThrowsAsync<FileDeleteException>(() => CreateService().DeleteAsync(Key));
    }

    [Fact]
    public async Task Upload_ToAnUnreachableEndpointIsAFileUploadException()
    {
        // Un puerto que estuvo abierto y ya no lo está: conexión rechazada.
        string closedUrl;
        await using (var closed = await HttpStubServer.StartAsync())
            closedUrl = closed.BaseUrl;

        await Assert.ThrowsAsync<FileUploadException>(
            () => CreateService(serviceUrl: closedUrl).UploadAsync(Key, new MemoryStream(PdfBytes), "application/pdf"));
    }
}
