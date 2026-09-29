namespace Revestik.Api.Models;

public sealed class InventoryCostLayer
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int SourceMovementId { get; set; }
    public InventoryMovement SourceMovement { get; set; } = null!;
    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}