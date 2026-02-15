using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE SERIALIZACIÓN (UUID v7 y Decimales)
// Esto asegura que los Guids se guarden como UUIDs estándar legibles en Mongo
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// 2. CARGA DE CONFIGURACIONES (IOptions Pattern)
builder.Services.Configure<DbSettings>(builder.Configuration.GetSection("DbSettings"));
builder.Services.Configure<TokenSettings>(builder.Configuration.GetSection("TokenSettings"));

// 3. AGREGAR SERVICIOS DE LA APLICACIÓN
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers(); // Necesario para la capa de Interfaces
builder.Services.AddOpenApi();     // Soporte nativo de OpenAPI de .NET 10

// 4. CONFIGURAR MEDIATR (Escaneando la capa de Application)
// Reemplaza 'Program' por alguna clase de tu capa Application si prefieres
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

// 5. CONFIGURAR EL PIPELINE HTTP
if (app.Environment.IsDevelopment())
{
    // Habilitar el endpoint de OpenAPI (json)
    app.MapOpenApi();
    
    // Configurar Scalar como interfaz de pruebas (reemplaza Swagger UI)
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Invoicing Service API")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

// 6. MAPEO DE CONTROLADORES
app.MapControllers();

app.Run();