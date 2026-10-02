namespace Revestik.Shared.Products;

public sealed record ProductListItemResponse(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    string Description,
    string CabysCode,
    string InventoryUnitSymbol,
    string CommercialUnitSymbol,
    decimal CommercialUnitsPerInventoryUnit,
    bool RequiresWholeInventoryUnits,
    decimal SalePrice,
    decimal CurrentCost,
    decimal TaxRate,
    decimal AvailableStock,
    decimal MinimumStock,
    bool IsDeleted)
{
    public SalePriceBasis SalePriceBasis { get; init; } =
        SalePriceBasis.InventoryUnit;
}