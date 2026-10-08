namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed class AuctionLifecycleConsumerSettings
{
    public string Topic { get; set; } = "investment.auction-lifecycle.v1";
    public string DeadLetterTopic { get; set; } = "invoicing.auction-lifecycle.dlq";
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public string AutoOffsetReset { get; set; } = "Earliest";
}
