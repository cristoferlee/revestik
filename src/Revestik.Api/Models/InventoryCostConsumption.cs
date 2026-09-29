namespace Revestik.Api.Models;

public sealed class InventoryCostConsumption
{
    public int Id { get; set; }
    public int InventoryMovementId { get; set; }
    public InventoryMovement InventoryMovement { get; set; } = null!;
    public int InventoryCostLayerId { get; set; }
    public InventoryCostLayer InventoryCostLayer { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? UnitCostSnapshot { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}