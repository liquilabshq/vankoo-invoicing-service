using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuctionLifecycleDeadLetterPublisher _deadLetterPublisher;
    private readonly KafkaSettings _kafkaSettings;
    private readonly AuctionLifecycleConsumerSettings _settings;
    private readonly ILogger<AuctionLifecycleConsumer> _logger;

    public AuctionLifecycleConsumer(
        IServiceScopeFactory scopeFactory,
        AuctionLifecycleDeadLetterPublisher deadLetterPublisher,
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<AuctionLifecycleConsumerSettings> settings,
        ILogger<AuctionLifecycleConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _deadLetterPublisher = deadLetterPublisher;
        _kafkaSettings = kafkaSettings.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaSettings.BootstrapServers))
        {
            _logger.LogWarning("KafkaSettings:BootstrapServers está vacío; el consumidor de {Topic} no se inicia.", _settings.Topic);
            return Task.CompletedTask;
        }

        // Consume() bloquea el hilo, así que el bucle corre fuera del pool para no consumir un hilo del host.
        return Task.Factory.StartNew(
                () => ConsumeLoopAsync(stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.ConsumerGroupId,
            ClientId = $"{_kafkaSettings.ClientId}-consumer",
            EnableAutoCommit = false,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.AutoOffsetReset, ignoreCase: true)
        };

        using var consumer = new ConsumerBuilder<string?, string>(config).Build();
        consumer.Subscribe(_settings.Topic);

        _logger.LogInformation(
            "Consumidor de ciclo de vida de subastas iniciado. Topic={Topic} Group={GroupId}",
            _settings.Topic, config.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string?, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    // Por ejemplo, el topic aún no existe: se reintenta sin tumbar el servicio.
                    _logger.LogWarning(ex, "Error al consumir {Topic}: {Reason}", _settings.Topic, ex.Error.Reason);
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _settings.RetryDelayMs)), stoppingToken);
                    continue;
                }

                if (result?.Message is null)
                    continue;

                await HandleMessageAsync(result, stoppingToken);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado ordenado del host.
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Consumidor de ciclo de vida de subastas detenido.");
        }
    }

    private async Task HandleMessageAsync(ConsumeResult<string?, string> result, CancellationToken stoppingToken)
    {
        string? invoiceId;
        try
        {
            invoiceId = AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(
                ReadEventTypeHeader(result.Message), result.Message.Value);
        }
        catch (InvalidAuctionLifecycleMessageException ex)
        {
            // Un mensaje mal formado no se arregla reintentando.
            _logger.LogError(ex, "Mensaje inválido en {Topic}[{Partition}]@{Offset}; se envía a DLQ.",
                result.Topic, result.Partition.Value, result.Offset.Value);
            await DeadLetterAsync(result, ex, stoppingToken);
            return;
        }

        if (invoiceId is null)
            return;

        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Max(1, _settings.MaxAttempts); attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new MarkInvoiceAsPublishedCommand(invoiceId), stoppingToken);

                _logger.LogInformation("Factura {InvoiceId} marcada como PUBLISHED por AuctionPublished.", invoiceId);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex,
                    "No se pudo procesar AuctionPublished de la factura {InvoiceId}. Intento {Attempt}/{MaxAttempts}",
                    invoiceId, attempt, _settings.MaxAttempts);

                if (attempt < _settings.MaxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _settings.RetryDelayMs)), stoppingToken);
            }
        }

        _logger.LogError(lastError,
            "AuctionPublished de la factura {InvoiceId} agotó {MaxAttempts} intentos; se envía a DLQ.",
            invoiceId, _settings.MaxAttempts);
        await DeadLetterAsync(result, lastError!, stoppingToken);
    }

    private async Task DeadLetterAsync(
        ConsumeResult<string?, string> result,
        Exception error,
        CancellationToken stoppingToken)
    {
        // Si la DLQ falla no se confirma el offset: se insiste hasta lograrlo para no perder el mensaje.
        while (true)
        {
            try
            {
                await _deadLetterPublisher.PublishAsync(result, error, stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo publicar en la DLQ {DeadLetterTopic}; se reintenta.", _settings.DeadLetterTopic);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _settings.RetryDelayMs)), stoppingToken);
            }
        }
    }

    private static string? ReadEventTypeHeader(Message<string?, string> message)
        => message.Headers.TryGetLastBytes(AuctionLifecycleMessageParser.EventTypeHeader, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
}
