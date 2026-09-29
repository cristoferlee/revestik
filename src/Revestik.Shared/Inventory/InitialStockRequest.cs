using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class InitialStockRequest
{
    [Range(
        typeof(decimal),
        "0",
        "99999999999999.9999",
        ErrorMessage = "El inventario inicial no puede ser negativo.")]
    public decimal Quantity { get; set; }

    [StringLength(
        1000,
        ErrorMessage = "La nota no puede superar los 1000 caracteres.")]
    public string Notes { get; set; } = string.Empty;
}