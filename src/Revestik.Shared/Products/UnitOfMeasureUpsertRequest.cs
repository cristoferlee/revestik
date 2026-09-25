using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Products;

public sealed class UnitOfMeasureUpsertRequest
{
    [Required(ErrorMessage = "El nombre de la unidad es obligatorio.")]
    [StringLength(
        100,
        ErrorMessage = "El nombre de la unidad no puede superar los 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El símbolo de la unidad es obligatorio.")]
    [StringLength(
        20,
        ErrorMessage = "El símbolo de la unidad no puede superar los 20 caracteres.")]
    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}