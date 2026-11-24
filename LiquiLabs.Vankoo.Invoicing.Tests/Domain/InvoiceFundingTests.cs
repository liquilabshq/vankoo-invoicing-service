using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Domain;

public sealed class InvoiceFundingTests
{
    [Fact]
    public void MarkFunded_MovesPublishedInvoiceToFunded()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkPublished();

        invoice.MarkFunded();

        Assert.Equal(InvoiceStatus.FUNDED, invoice.Status);
    }

    [Fact]
    public void MarkFunded_RejectsInvoiceThatWasNeverPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();

        Assert.Throws<InvalidInvoiceStateException>(invoice.MarkFunded);
        Assert.Equal(InvoiceStatus.CONSISTENCY_PASSED, invoice.Status);
    }

    [Fact]
    public void MarkFunded_RejectsInvoiceThatIsAlreadyFunded()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkPublished();
        invoice.MarkFunded();

        Assert.Throws<InvalidInvoiceStateException>(invoice.MarkFunded);
    }
}
