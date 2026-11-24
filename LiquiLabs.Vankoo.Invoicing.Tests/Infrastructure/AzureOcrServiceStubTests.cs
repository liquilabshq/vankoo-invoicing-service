using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Stubs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Stubs.HttpStubServer;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

/// <summary>
/// Pruebas de integración de <see cref="AzureOcrService"/> contra un stub HTTP de
/// Azure AI Document Intelligence (API 2024-11-30, modelo <c>prebuilt-invoice</c>).
/// </summary>
/// <remarks>
/// Se usa el <c>DocumentIntelligenceClient</c> real apuntado al stub, por lo que se
/// ejercita la operación de larga duración completa: <c>POST ...:analyze</c> responde
/// <c>202</c> con <c>Operation-Location</c> y el SDK sondea esa URL hasta
/// <c>succeeded</c>. También se verifica la traducción de errores HTTP a excepciones
/// del dominio de OCR y los reintentos del SDK ante errores transitorios.
/// </remarks>
/// <author>LiquiLabs</author>
public sealed class AzureOcrServiceStubTests : IAsyncLifetime
{
    private const string ApiKey = "test-document-intelligence-key";
    private const string OperationId = "3f6c1e9a-0d2b-4c7e-9a51-2e8d3c7f6a10";
    private static readonly byte[] InvoicePdf = "%PDF-1.4 factura E001-28"u8.ToArray();

    private HttpStubServer _azure = null!;

    public async Task InitializeAsync() => _azure = await HttpStubServer.StartAsync();

    public async Task DisposeAsync() => await _azure.DisposeAsync();

    private AzureOcrService CreateService() => new(
        Options.Create(new AzureOcrSettings { Endpoint = _azure.BaseUrl + "/", ApiKey = ApiKey }),
        new AzureOcrMapper(NullLogger<AzureOcrMapper>.Instance, new InvoiceLineItemResolver()),
        NullLogger<AzureOcrService>.Instance);

    private static StubResponse Accepted(string baseUrl) => new(
        202,
        Headers: new Dictionary<string, string>
        {
            ["Operation-Location"] =
                $"{baseUrl}/documentintelligence/documentModels/prebuilt-invoice/analyzeResults/{OperationId}?api-version=2024-11-30",
            ["Retry-After"] = "0"
        });

    private static StubResponse AzureError(int status, string code, string message) => new(
        status,
        $$$"""{"error":{"code":"{{{code}}}","message":"{{{message}}}"}}""",
        Headers: new Dictionary<string, string> { ["Retry-After"] = "0" });

    private static StubResponse Running() => new(
        200,
        """{"status":"running","createdDateTime":"2026-11-24T10:00:00Z","lastUpdatedDateTime":"2026-11-24T10:00:01Z"}""",
        Headers: new Dictionary<string, string> { ["Retry-After"] = "0" });

    private static StubResponse Succeeded(string documents) => new(200, $$"""
        {
          "status": "succeeded",
          "createdDateTime": "2026-11-24T10:00:00Z",
          "lastUpdatedDateTime": "2026-11-24T10:00:02Z",
          "analyzeResult": {
            "apiVersion": "2024-11-30",
            "modelId": "prebuilt-invoice",
            "stringIndexType": "textElements",
            "content": "FACTURA ELECTRONICA E001-28",
            "pages": [],
            "documents": {{documents}}
          }
        }
        """);

    private const string InvoiceDocument = """
        [{
          "docType": "invoice",
          "spans": [],
          "confidence": 0.97,
          "fields": {
            "InvoiceId":     {"type": "string", "valueString": "E001-28", "content": "E001-28", "confidence": 0.95},
            "InvoiceDate":   {"type": "date", "valueDate": "2026-11-02", "content": "02/11/2026", "confidence": 0.93},
            "DueDate":       {"type": "date", "valueDate": "2026-12-02", "content": "02/12/2026", "confidence": 0.92},
            "VendorTaxId":   {"type": "string", "valueString": "20601144311", "content": "20601144311", "confidence": 0.96},
            "VendorName":    {"type": "string", "valueString": "GRUPO CREATIVO PYME E.I.R.L.", "content": "GRUPO CREATIVO PYME E.I.R.L.", "confidence": 0.9},
            "CustomerTaxId": {"type": "string", "valueString": "20100047218", "content": "20100047218", "confidence": 0.95},
            "CustomerName":  {"type": "string", "valueString": "COMERCIAL ANDINA S.A.C.", "content": "COMERCIAL ANDINA S.A.C.", "confidence": 0.94},
            "SubTotal":      {"type": "currency", "valueCurrency": {"amount": 1000, "currencySymbol": "S/", "currencyCode": "PEN"}, "content": "S/ 1000.00", "confidence": 0.94},
            "TotalTax":      {"type": "currency", "valueCurrency": {"amount": 180, "currencySymbol": "S/", "currencyCode": "PEN"}, "content": "S/ 180.00", "confidence": 0.93},
            "InvoiceTotal":  {"type": "currency", "valueCurrency": {"amount": 1180, "currencySymbol": "S/", "currencyCode": "PEN"}, "content": "S/ 1180.00", "confidence": 0.95},
            "Items": {"type": "array", "valueArray": [{
              "type": "object", "confidence": 0.9, "content": "",
              "valueObject": {
                "Description": {"type": "string", "valueString": "SERVICIO DE DISEÑO", "content": "SERVICIO DE DISEÑO", "confidence": 0.93},
                "Quantity":    {"type": "number", "valueNumber": 10, "content": "10", "confidence": 0.94},
                "UnitPrice":   {"type": "currency", "valueCurrency": {"amount": 100, "currencyCode": "PEN"}, "content": "100.00", "confidence": 0.92},
                "Amount":      {"type": "currency", "valueCurrency": {"amount": 1000, "currencyCode": "PEN"}, "content": "1000.00", "confidence": 0.92}
              }
            }]}
          }
        }]
        """;

    [Fact]
    public async Task Extract_RunsTheLongRunningAnalysisAndMapsTheInvoice()
    {
        _azure.WillRespond(Accepted).WillRespond(Running()).WillRespond(Succeeded(InvoiceDocument));

        var result = await CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf));

        Assert.Equal("20601144311", result.IssuerData.Ruc.Value);
        Assert.Equal("20100047218", result.PayerData.Ruc.Value);
        Assert.Equal("COMERCIAL ANDINA S.A.C.", result.PayerData.LegalName);
        Assert.Equal(1180m, result.Amounts.Total.Amount);
        Assert.Equal(1000m, Assert.Single(result.Items).Subtotal.Amount);

        // 1 POST de análisis + 2 sondeos (running -> succeeded) sobre Operation-Location.
        var requests = _azure.Requests;
        Assert.Equal(3, requests.Count);
        Assert.Equal("POST", requests[0].Method);
        Assert.StartsWith("/documentintelligence/documentModels/prebuilt-invoice:analyze", requests[0].PathAndQuery);
        Assert.Contains("api-version=2024-11-30", requests[0].PathAndQuery);
        Assert.All(requests.Skip(1), poll =>
        {
            Assert.Equal("GET", poll.Method);
            Assert.Contains($"/analyzeResults/{OperationId}", poll.PathAndQuery);
        });
    }

    [Fact]
    public async Task Extract_AuthenticatesWithTheKeyAndSendsTheDocument()
    {
        _azure.WillRespond(Accepted).WillRespond(Succeeded(InvoiceDocument));

        await CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf));

        var analyze = _azure.Requests[0];
        Assert.Equal(ApiKey, analyze.Header("Ocp-Apim-Subscription-Key"));
        Assert.Contains(Convert.ToBase64String(InvoicePdf), analyze.BodyText);
    }

    [Fact]
    public async Task Extract_WithoutDocumentsIsAnInvalidDocument()
    {
        _azure.WillRespond(Accepted).WillRespond(Succeeded("[]"));

        var error = await Assert.ThrowsAsync<OcrProcessingException>(
            () => CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf)));

        Assert.Equal(OcrErrorCode.InvalidDocument, error.ErrorCode);
        Assert.False(error.IsTransient);
    }

    [Theory]
    [InlineData(400, "InvalidRequest")]
    [InlineData(415, "UnsupportedMediaType")]
    public async Task Extract_RejectedDocumentsAreNotRetried(int status, string code)
    {
        _azure.WillRespond(AzureError(status, code, "The file is corrupted or format is unsupported."));

        var error = await Assert.ThrowsAsync<OcrInvalidDocumentException>(
            () => CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf)));

        Assert.False(error.IsTransient);
        Assert.Single(_azure.Requests);
    }

    [Fact]
    public async Task Extract_RateLimitIsRetriedBySdkAndThenReportedAsTransient()
    {
        for (var i = 0; i < 4; i++)
            _azure.WillRespond(AzureError(429, "TooManyRequests", "Rate limit exceeded."));

        var error = await Assert.ThrowsAsync<OcrProcessingException>(
            () => CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf)));

        Assert.Equal(OcrErrorCode.RateLimitExceeded, error.ErrorCode);
        Assert.True(error.IsTransient);
        // Política por defecto de Azure.Core: 1 intento + 3 reintentos.
        Assert.Equal(4, _azure.Requests.Count);
    }

    [Fact]
    public async Task Extract_RecoversWhenATransientErrorIsFollowedBySuccess()
    {
        _azure.WillRespond(AzureError(503, "ServiceUnavailable", "Try again later."))
            .WillRespond(Accepted)
            .WillRespond(Succeeded(InvoiceDocument));

        var result = await CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf));

        Assert.Equal(1180m, result.Amounts.Total.Amount);
        Assert.Equal(3, _azure.Requests.Count);
    }

    [Fact]
    public async Task Extract_PersistentServerErrorsAreTransientServiceErrors()
    {
        for (var i = 0; i < 4; i++)
            _azure.WillRespond(AzureError(500, "InternalServerError", "An unexpected error occurred."));

        var error = await Assert.ThrowsAsync<OcrProcessingException>(
            () => CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf)));

        Assert.Equal(OcrErrorCode.ServiceError, error.ErrorCode);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task Extract_AnInvalidKeyIsAPermanentUnknownFailure()
    {
        _azure.WillRespond(AzureError(401, "401", "Access denied due to invalid subscription key."));

        var error = await Assert.ThrowsAsync<OcrProcessingException>(
            () => CreateService().ExtractInvoiceDataAsync(new MemoryStream(InvoicePdf)));

        Assert.Equal(OcrErrorCode.Unknown, error.ErrorCode);
        Assert.False(error.IsTransient);
        Assert.DoesNotContain(ApiKey, error.Message);
    }

    [Theory]
    [InlineData("", ApiKey)]
    [InlineData("http://localhost:5000/", "")]
    [InlineData("not-a-url", ApiKey)]
    public void Constructor_RejectsAnIncompleteConfiguration(string endpoint, string apiKey)
    {
        Assert.Throws<InvalidOperationException>(() => new AzureOcrService(
            Options.Create(new AzureOcrSettings { Endpoint = endpoint, ApiKey = apiKey }),
            new AzureOcrMapper(NullLogger<AzureOcrMapper>.Instance, new InvoiceLineItemResolver()),
            NullLogger<AzureOcrService>.Instance));
    }
}
