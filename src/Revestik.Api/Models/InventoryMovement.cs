using Revestik.Api.Models.Identity;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Models;

public sealed class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public InventoryMovementType Type { get; set; }

    public decimal QuantityChange { get; set; }

    public decimal StockBefore { get; set; }

    public decimal StockAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public InventoryAdjustmentReason? AdjustmentReason { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}