using Revestik.Shared.Products;

namespace Revestik.Shared.Inventory;

public sealed class InventoryProductCreateRequest
{
    public ProductUpsertRequest Product { get; set; } = new();

    public InitialStockRequest InitialStock { get; set; } = new();
}