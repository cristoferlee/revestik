namespace Revestik.Shared.Products;

public sealed record ProductListItemResponse(
    int Id,
    string Description,
    string CabysCode,
    string Unit,
    decimal SalePrice,
    decimal TaxRate,
    decimal StockQuantity);