namespace Revestik.Api.Models;

public sealed class Product
{
    public int Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? CabysCode { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal SalePrice { get; set; }

    public decimal TaxRate { get; set; } = 13m;

    public decimal StockQuantity { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}