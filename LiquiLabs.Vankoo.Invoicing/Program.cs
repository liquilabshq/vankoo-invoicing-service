using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Behaviors;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using Amazon.Runtime;
using Amazon.S3;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Repositories;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Workers;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Web;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
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
builder.Services.Configure<AzureOcrSettings>(builder.Configuration.GetSection("AzureOcrSettings")); // Azure OCR
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaSettings")); // Kafka
builder.Services.Configure<OcrWorkerSettings>(builder.Configuration.GetSection("OcrWorkerSettings"));

builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("MinioSettings"));

// Límite de tamaño de archivo: el framework rechaza requests que superen esto antes de llegar al dominio
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// 3. CLIENTE S3 (MinIO)
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
    var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
    var config = new AmazonS3Config
    {
        ServiceURL = $"{(settings.UseSSL ? "https" : "http")}://{settings.Endpoint}",
        ForcePathStyle = true  // Requerido por MinIO
    };
    return new AmazonS3Client(credentials, config);
});
builder.Services.AddScoped<IStorageService, MinioStorageService>();

// 4. AGREGAR SERVICIOS DE LA APLICACIÓN
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers(); // Necesario para la capa de Interfaces
builder.Services.AddOpenApi();     // Soporte nativo de OpenAPI de .NET 10

// 4. Registrar MediatR y el Behavior de validación
builder.Services.AddMediatR(config => {
    // Busca todos los Comandos/Handlers en este proyecto
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Conecta el ValidationBehavior al flujo (Pipeline)
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// Registrar FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Registrar el Repositorio de MongoDB
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IOcrTaskRepository, OcrTaskRepository>();

// Registrar los Servicios de Dominio/Aplicación
builder.Services.AddScoped<IOcrService, AzureOcrService>();
builder.Services.AddScoped<IStorageService, MinioStorageService>();
builder.Services.AddSingleton<AzureOcrMapper>();
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<IEventBus, KafkaEventBus>();
builder.Services.AddHostedService<OcrTaskWorker>();

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

app.UseExceptionHandler();
app.UseHttpsRedirection();

// 6. MAPEO DE CONTROLADORES
app.MapControllers();

app.Run();