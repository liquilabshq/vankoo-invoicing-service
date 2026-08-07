using LiquiLabs.Vankoo.Invoicing.Application.Commands;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteAllInvoices;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Resources;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;
    
    public InvoicesController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadInvoice(
        [FromForm] UploadInvoiceForm form,
        CancellationToken cancellationToken)
    {
        if (form.File.Length == 0)
            return BadRequest("El archivo de factura está vacío.");

        await using var stream = form.File.OpenReadStream();
        var invoiceId = await _mediator.Send(new UploadInvoiceCommand
        {
            MypeId = form.MypeId,
            OriginalName = form.File.FileName,
            ContentType = form.File.ContentType,
            FileSizeBytes = form.File.Length,
            FileStream = stream
        }, cancellationToken);

        return CreatedAtAction(
            nameof(GetInvoiceById),
            new { id = invoiceId },
            new { InvoiceId = invoiceId, Status = "UPLOADED" });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoiceById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInvoice(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteInvoiceCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAllInvoices(
        [FromQuery] bool confirm,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (!confirm)
        {
            return BadRequest(new
            {
                ErrorCode = "DELETION_CONFIRMATION_REQUIRED",
                Message = "Set confirm=true to delete every invoice and its stored document."
            });
        }

        var deletedCount = await _mediator.Send(new DeleteAllInvoicesCommand(), cancellationToken);
        return Ok(new { DeletedCount = deletedCount });
    }
    
    // POST: api/v1/invoices/{id}/ocr/sync
    [HttpPost("{id}/ocr/sync")]
    public async Task<IActionResult> ProcessOcrSync(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var command = new ProcessOcrSynchronouslyCommand(id);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }
    
    [HttpPost("upload-local")]
    public async Task<IActionResult> UploadInvoiceFromLocalPath(
        [FromQuery] string mypeId,
        [FromQuery] string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mypeId))
            return BadRequest("El MypeId es requerido.");

        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest("El filePath es requerido.");

        var command = new UploadInvoicePruebaCommand(mypeId, filePath);
        var invoiceId = await _mediator.Send(command, cancellationToken);

        return Ok(new
        {
            InvoiceId = invoiceId,
            Message = "Factura leída desde disco local y subida correctamente."
        });
    }
}


