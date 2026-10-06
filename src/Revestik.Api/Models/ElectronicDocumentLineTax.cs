namespace Revestik.Api.Models;

public sealed class ElectronicDocumentLineTax
{
    public int Id { get; set; }
    public int ElectronicDocumentLineId { get; set; }
    public ElectronicDocumentLine ElectronicDocumentLine { get; set; } = null!;
    public string TaxCode { get; set; } = string.Empty;
    public string VatRateCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
