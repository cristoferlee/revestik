using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Products;

public sealed class ProductListRequest
{
    [StringLength(
        150,
        ErrorMessage = "La búsqueda no puede superar los 150 caracteres.")]
    public string? Search { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La categoría no es válida.")]
    public int? CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La unidad no es válida.")]
    public int? UnitId { get; set; }

    [EnumDataType(
        typeof(ProductActivityStatus),
        ErrorMessage = "El estado del producto no es válido.")]
    public ProductActivityStatus? ActivityStatus { get; set; } =
        ProductActivityStatus.Active;

    [EnumDataType(
        typeof(ProductStockStatus),
        ErrorMessage = "El estado de inventario no es válido.")]
    public ProductStockStatus? StockStatus { get; set; } =
        ProductStockStatus.All;

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "El número de página debe ser mayor que cero.")]
    public int Page { get; set; } = 1;

    [Range(
        1,
        100,
        ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; set; } = 20;
}
