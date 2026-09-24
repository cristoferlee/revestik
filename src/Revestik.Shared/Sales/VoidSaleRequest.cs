using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Sales;

public sealed class VoidSaleRequest
{
    [Required(ErrorMessage = "Debe indicar el motivo de la anulación.")]
    [StringLength(
        1000,
        MinimumLength = 3,
        ErrorMessage = "El motivo de la anulación debe tener entre 3 y 1000 caracteres.")]
    public string Reason { get; set; } = string.Empty;
}