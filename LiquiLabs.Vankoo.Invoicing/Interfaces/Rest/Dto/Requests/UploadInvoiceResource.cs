namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Requests;

public class UploadInvoiceResource
{
    public IFormFile File { get; set; } = null!;
    public string MypeId { get; set; } = null!;
}
