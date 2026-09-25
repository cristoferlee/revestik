using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Products;

public sealed class ProductCategoryUpsertRequest
{
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(
        100,
        ErrorMessage = "El nombre de la categoría no puede superar los 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}