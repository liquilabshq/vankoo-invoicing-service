using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

/// <summary>
/// Lee CompanyKycStatusChanged de vankoo.profile.events y guarda el estado KYC de cada MYPE.
/// Confirma el offset solo después de guardarlo; si Mongo no responde, reintenta el mismo
/// mensaje con espera creciente. Los demás eventos de Profile y los mensajes inválidos se saltan.
/// </summary>
public sealed class ProfileKycStatusConsumer : BackgroundService
{
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _settings;
    private readonly ILogger<ProfileKycStatusConsumer> _logger;

    public ProfileKycStatusConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> settings,
        ILogger<ProfileKycStatusConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    // Consume() bloquea el hilo: el bucle corre en su propio hilo para no frenar el arranque del host.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => ConsumeAsync(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        if (!_settings.ProfileKycConsumerEnabled)
        {
            _logger.LogInformation("Consumidor de KYC de Profile apagado (ProfileKycConsumerEnabled=false)");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ProfileKycGroupId,
            ClientId = $"{_settings.ClientId}-profile-kyc",
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.ProfileKycAutoOffsetReset, ignoreCase: true),
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_settings.ProfileEventsTopic);
        _logger.LogInformation("Consumiendo {Topic} como grupo {GroupId}", _settings.ProfileEventsTopic, _settings.ProfileKycGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("No se pudo leer {Topic}: {Reason}", _settings.ProfileEventsTopic, ex.Error.Reason);
                    await Task.Delay(FirstRetryDelay, stoppingToken);
                    continue;
                }

                await ApplyUntilDoneAsync(result, stoppingToken);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ApplyUntilDoneAsync(ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        var (kind, integrationEvent, error) = CompanyKycStatusChangedIntegrationEvent.Parse(result.Message.Value);
        if (kind == ProfileEventKind.Ignored)
            return;
        if (kind == ProfileEventKind.Invalid || integrationEvent is null)
        {
            _logger.LogWarning("Mensaje {TopicPartitionOffset} saltado: {Error}", result.TopicPartitionOffset, error);
            return;
        }

        var delay = FirstRetryDelay;
        while (true)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new RecordMypeKycStatusCommand(
                    integrationEvent.UserId,
                    integrationEvent.CompanyId,
                    integrationEvent.KycStatus,
                    integrationEvent.OccurredAtUtc), stoppingToken);
                _logger.LogInformation("KYC de la MYPE {MypeId}: {Status}", integrationEvent.UserId, integrationEvent.KycStatus);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo guardar el KYC de {TopicPartitionOffset}; reintento en {Delay}s",
                    result.TopicPartitionOffset, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }
}
