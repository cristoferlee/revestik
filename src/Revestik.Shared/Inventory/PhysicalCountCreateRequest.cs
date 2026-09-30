using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class PhysicalCountCreateRequest
{
    [StringLength(
        1000,
        ErrorMessage = "La nota no puede superar los 1000 caracteres.")]
    public string Notes { get; set; } = string.Empty;
}