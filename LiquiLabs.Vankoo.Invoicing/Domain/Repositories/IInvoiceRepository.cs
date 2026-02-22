using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Repositories;

public interface IInvoiceRepository
{
    // ========== COMMANDS ==========
    
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);

    // ========== QUERIES==========

    Task<Invoice?> GetByIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Invoice>> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default);
    
    Task<Invoice?> GetByOcrOperationIdAsync(OcrOperationId  operationId, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(InvoiceId id, CancellationToken cancellationToken = default);
}