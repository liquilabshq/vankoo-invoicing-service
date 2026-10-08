using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleDeadLetterPublisher : IDisposable
{
    private readonly IProducer<string?, string> _producer;
    private readonly string _deadLetterTopic;

    public AuctionLifecycleDeadLetterPublisher(
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<AuctionLifecycleConsumerSettings> consumerSettings)
    {
        var kafka = kafkaSettings.Value;
        _deadLetterTopic = consumerSettings.Value.DeadLetterTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            ClientId = $"{kafka.ClientId}-dlq",
            Acks = Enum.Parse<Acks>(kafka.Acks, ignoreCase: true),
            EnableIdempotence = kafka.EnableIdempotence,
            MessageTimeoutMs = kafka.MessageTimeoutMs,
            MessageSendMaxRetries = kafka.Retries
        };

        _producer = new ProducerBuilder<string?, string>(config).Build();
    }

    public async Task PublishAsync(
        ConsumeResult<string?, string> original,
        Exception error,
        CancellationToken cancellationToken)
    {
        var headers = new Headers();
        foreach (var header in original.Message.Headers)
            headers.Add(header.Key, header.GetValueBytes());

        headers.Add("x-original-topic", Encoding.UTF8.GetBytes(original.Topic));
        headers.Add("x-original-partition", Encoding.UTF8.GetBytes(original.Partition.Value.ToString()));
        headers.Add("x-original-offset", Encoding.UTF8.GetBytes(original.Offset.Value.ToString()));
        headers.Add("x-error", Encoding.UTF8.GetBytes(error.Message));

        await _producer.ProduceAsync(
            _deadLetterTopic,
            new Message<string?, string>
            {
                Key = original.Message.Key,
                Value = original.Message.Value,
                Headers = headers
            },
            cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
