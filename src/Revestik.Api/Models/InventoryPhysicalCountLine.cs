namespace Revestik.Api.Models;

public sealed class InventoryPhysicalCountLine
{
    public int Id { get; set; }

    public int PhysicalCountId { get; set; }

    public InventoryPhysicalCount PhysicalCount { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public decimal ExpectedQuantity { get; set; }

    public decimal? CountedQuantity { get; set; }
}