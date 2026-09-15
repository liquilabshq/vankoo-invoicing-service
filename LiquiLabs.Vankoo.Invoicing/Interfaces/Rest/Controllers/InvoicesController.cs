using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Requests;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Responses;
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
    [ProducesResponseType(typeof(InvoiceResource), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadInvoice(
        [FromForm] UploadInvoiceResource request,
        CancellationToken ct)
    {
        // El API Gateway valida el JWT y agrega este header (BearerAuthorizationRequest
        // en vankoo-api-gateway) antes de reenviar — sin esa capa, no llega.
        var mypeId = Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(mypeId))
            return Unauthorized();

        var command = new UploadInvoiceCommand
        {
            MypeId = mypeId,
            OriginalName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            FileStream = request.File.OpenReadStream()
        };

        var invoiceId = await _mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(DownloadInvoiceFile),
            new { id = invoiceId },
            new InvoiceResource { InvoiceId = invoiceId });
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


