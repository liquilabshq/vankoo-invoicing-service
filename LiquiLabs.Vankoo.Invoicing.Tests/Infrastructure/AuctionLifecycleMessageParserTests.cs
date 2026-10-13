using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class AuctionLifecycleMessageParserTests
{
    private const string InvoiceId = "30000000-0000-0000-0000-000000000001";

    [Fact]
    public void TryGetPublishedInvoiceId_ReturnsInvoiceIdForAuctionPublishedHeader()
    {
        var invoiceId = AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(
            "AuctionPublished", Envelope("AuctionPublished", InvoiceId));

        Assert.Equal(InvoiceId, invoiceId);
    }

    [Fact]
    public void TryGetPublishedInvoiceId_FallsBackToBodyEventTypeWhenHeaderIsMissing()
    {
        var invoiceId = AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(
            null, Envelope("AuctionPublished", InvoiceId));

        Assert.Equal(InvoiceId, invoiceId);
    }

    [Theory]
    [InlineData("AuctionCreated")]
    [InlineData("PartitionAdded")]
    [InlineData("AuctionFullyFunded")]
    [InlineData("AuctionClosed")]
    public void TryGetPublishedInvoiceId_IgnoresOtherEventTypesFromTheHeader(string eventType)
    {
        var invoiceId = AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(eventType, "no es json");

        Assert.Null(invoiceId);
    }

    [Fact]
    public void TryGetPublishedInvoiceId_IgnoresOtherEventTypesFromTheBody()
    {
        var invoiceId = AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(
            null, Envelope("PartitionAdded", InvoiceId));

        Assert.Null(invoiceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("[]")]
    public void TryGetPublishedInvoiceId_RejectsBodiesThatAreNotAJsonObject(string body)
    {
        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryGetPublishedInvoiceId("AuctionPublished", body));
    }

    [Fact]
    public void TryGetPublishedInvoiceId_RejectsUnsupportedSchemaVersion()
    {
        var body = Envelope("AuctionPublished", InvoiceId, schemaVersion: 2);

        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryGetPublishedInvoiceId("AuctionPublished", body));
    }

    [Fact]
    public void TryGetPublishedInvoiceId_RejectsInvoiceIdThatIsNotAUuid()
    {
        var body = Envelope("AuctionPublished", "no-es-uuid");

        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryGetPublishedInvoiceId("AuctionPublished", body));
    }

    [Fact]
    public void TryGetPublishedInvoiceId_RejectsMessageWithoutEventTypeAnywhere()
    {
        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryGetPublishedInvoiceId(null, "{\"schemaVersion\":1}"));
    }

    private static string Envelope(string eventType, string invoiceId, int schemaVersion = 1)
        => $$"""
           {
             "eventId": "10000000-0000-0000-0000-000000000002",
             "eventType": "{{eventType}}",
             "schemaVersion": {{schemaVersion}},
             "aggregateId": "20000000-0000-0000-0000-000000000001",
             "sequence": 2,
             "occurredAt": "2026-09-14T16:00:00Z",
             "data": { "invoiceId": "{{invoiceId}}", "mypeId": "40000000-0000-0000-0000-000000000001" }
           }
           """;
}
