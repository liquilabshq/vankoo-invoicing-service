using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsFunded;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class MarkInvoiceAsFundedCommandHandlerTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<InvoiceId> SeedPublishedAsync()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkPublished();
        await _host.Invoices.SaveAsync(invoice);
        return invoice.Id;
    }

    [Fact]
    public async Task Handle_MarksPublishedInvoiceAsFunded()
    {
        var invoiceId = await SeedPublishedAsync();

        await _host.SendAsync(new MarkInvoiceAsFundedCommand(invoiceId.Value));

        var stored = await _host.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(InvoiceStatus.FUNDED, stored!.Status);
    }

    [Fact]
    public async Task Handle_IsIdempotentWhenTheEventIsDeliveredTwice()
    {
        var invoiceId = await SeedPublishedAsync();
        var command = new MarkInvoiceAsFundedCommand(invoiceId.Value);

        await _host.SendAsync(command);
        var fundedAt = (await _host.Invoices.GetByIdAsync(invoiceId))!.UpdatedAt;
        await _host.SendAsync(command);

        var stored = await _host.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(InvoiceStatus.FUNDED, stored!.Status);
        Assert.Equal(fundedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task Handle_ALatePublishedDoesNotUndoTheFunding()
    {
        var invoiceId = await SeedPublishedAsync();
        await _host.SendAsync(new MarkInvoiceAsFundedCommand(invoiceId.Value));

        await _host.SendAsync(new MarkInvoiceAsPublishedCommand(invoiceId.Value));

        var stored = await _host.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(InvoiceStatus.FUNDED, stored!.Status);
    }

    [Fact]
    public async Task Handle_FailsWhenTheInvoiceWasNeverPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);

        await Assert.ThrowsAsync<InvalidInvoiceStateException>(
            () => _host.SendAsync(new MarkInvoiceAsFundedCommand(invoice.Id.Value)));
    }

    [Fact]
    public async Task Handle_RejectsAnInvoiceIdThatIsNotAUuid()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _host.SendAsync(new MarkInvoiceAsFundedCommand("no-es-un-uuid")));
    }
}
