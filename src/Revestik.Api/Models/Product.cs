namespace Revestik.Api.Models;

public sealed class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public ProductCategory Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string CabysCode { get; set; } = string.Empty;

    public int InventoryUnitId { get; set; }

    public UnitOfMeasure InventoryUnit { get; set; } = null!;

    public int CommercialUnitId { get; set; }

    public UnitOfMeasure CommercialUnit { get; set; } = null!;

    public decimal CommercialUnitsPerInventoryUnit { get; set; } = 1m;

    public bool RequiresWholeInventoryUnits { get; set; }

    public decimal SalePrice { get; set; }

    public decimal CurrentCost { get; set; }

    public decimal TaxRate { get; set; } = 13m;

    public decimal StockQuantity { get; set; }

    public decimal MinimumStock { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}