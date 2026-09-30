using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class ResolveInventoryCostRequest
{
    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "El costo unitario debe ser mayor que cero.")]
    public decimal UnitCost { get; set; }
}