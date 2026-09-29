using Revestik.Api.Models.Identity;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Models;

public sealed class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int? SaleId { get; set; }

    public Sale? Sale { get; set; }

    public int? ReversesInventoryMovementId { get; set; }

    public InventoryMovement? ReversesInventoryMovement { get; set; }

    public InventoryMovement? ReversalInventoryMovement { get; set; }

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