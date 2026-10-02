using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Purchases;

public sealed class VoidPurchasePaymentRequest
{
    [Required(
        ErrorMessage = "Debe indicar el motivo de la anulación del pago.")]
    [StringLength(
        1000,
        MinimumLength = 3,
        ErrorMessage = "El motivo debe tener entre 3 y 1000 caracteres.")]
    public string Reason { get; set; } = string.Empty;
}