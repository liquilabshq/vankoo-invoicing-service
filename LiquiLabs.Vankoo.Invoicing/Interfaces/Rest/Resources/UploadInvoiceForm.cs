using Microsoft.AspNetCore.Mvc;

namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Resources;

public sealed class UploadInvoiceForm
{
    [FromForm(Name = "mypeId")]
    public string MypeId { get; init; } = string.Empty;

    [FromForm(Name = "file")]
    public IFormFile File { get; init; } = default!;
}
