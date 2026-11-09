using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Products;

public class ProductSubcategoryUpsertRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una categoría válida.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "El nombre de la subcategoría es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
