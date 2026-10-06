namespace Revestik.Api.Models;

public sealed class ElectronicDocumentLineDiscount
{
    public int Id { get; set; }
    public int ElectronicDocumentLineId { get; set; }
    public ElectronicDocumentLine ElectronicDocumentLine { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Nature { get; set; } = string.Empty;
}
