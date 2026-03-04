using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // POST: api/v1/invoices
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadInvoice(
        IFormFile file,
        [FromForm] string mypeId,
        CancellationToken ct)
    {
        var command = new UploadInvoiceCommand(
            mypeId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream());

        var invoiceId = await _mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(DownloadInvoiceFile),
            new { id = invoiceId },
            new { InvoiceId = invoiceId });
    }

    // GET: api/v1/invoices/{id}/file
    [HttpGet("{id}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoiceFile([FromRoute] string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DownloadInvoiceFileQuery(id), ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    // POST: api/v1/invoices/{id}/ocr/sync
    [HttpPost("{id}/ocr/sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessOcrSync([FromRoute] string id, CancellationToken ct)
    {
        await _mediator.Send(new ProcessOcrSynchronouslyCommand(id), ct);
        return Ok(new { Message = "OCR síncrono procesado correctamente." });
    }
}


