using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
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

    // POST: api/v1/invoices/{id}/ocr/sync
    [HttpPost("{id}/ocr/sync")]
    public async Task<IActionResult> ProcessOcrSync([FromRoute] string id)
    {
        var command = new ProcessOcrSynchronouslyCommand(id);
        await _mediator.Send(command);
        return Ok(new { Message = "OCR síncrono procesado correctamente." });
    }
    
}


