using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Stubs;

/// <summary>
/// Servidor HTTP local (Kestrel, puerto efímero) que hace de stub de un servicio
/// externo: Amazon S3 o Azure Document Intelligence.
/// </summary>
/// <remarks>
/// El adaptador bajo prueba usa el SDK real del proveedor apuntado a este servidor,
/// así que se valida el contrato de red completo (ruta, verbo, encabezados, cuerpo y
/// lectura de la respuesta) sin credenciales reales ni acceso a internet. Las
/// respuestas se encolan en orden y cada solicitud queda registrada.
/// </remarks>
/// <author>LiquiLabs</author>
public sealed class HttpStubServer : IAsyncDisposable
{
    public sealed record RecordedRequest(
        string Method,
        string PathAndQuery,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Body)
    {
        public string? Header(string name)
            => Headers.TryGetValue(name, out var value) ? value : null;

        public string BodyText => System.Text.Encoding.UTF8.GetString(Body);
    }

    public sealed record StubResponse(
        int Status,
        string? Body = null,
        string ContentType = "application/json",
        IReadOnlyDictionary<string, string>? Headers = null,
        byte[]? BinaryBody = null);

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<Func<string, StubResponse>> _responses = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    private HttpStubServer(WebApplication app)
    {
        _app = app;
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public IReadOnlyList<RecordedRequest> Requests => _requests.ToArray();

    public static async Task<HttpStubServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var stub = new HttpStubServer(app);
        app.Run(stub.HandleAsync);

        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
        stub.BaseUrl = addresses.First().TrimEnd('/');
        return stub;
    }

    /// <summary>Encola una respuesta fija.</summary>
    public HttpStubServer WillRespond(StubResponse response)
    {
        _responses.Enqueue(_ => response);
        return this;
    }

    /// <summary>Encola una respuesta que depende de la URL base (p. ej. un Operation-Location).</summary>
    public HttpStubServer WillRespond(Func<string, StubResponse> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    private async Task HandleAsync(HttpContext context)
    {
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer);

        var headers = context.Request.Headers.ToDictionary(
            h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        _requests.Enqueue(new RecordedRequest(
            context.Request.Method,
            context.Request.Path + context.Request.QueryString,
            headers,
            buffer.ToArray()));

        var response = _responses.TryDequeue(out var next)
            ? next(BaseUrl)
            : new StubResponse(500, """{"error":{"code":"NoStub","message":"No stubbed response"}}""");

        context.Response.StatusCode = response.Status;
        foreach (var (name, value) in response.Headers ?? new Dictionary<string, string>())
            context.Response.Headers[name] = value;

        var body = response.BinaryBody
                   ?? (response.Body is null ? null : System.Text.Encoding.UTF8.GetBytes(response.Body));
        if (body is not null)
        {
            context.Response.ContentType = response.ContentType;
            context.Response.ContentLength = body.Length;
            await context.Response.Body.WriteAsync(body);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
