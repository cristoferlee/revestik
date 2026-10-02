using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Suppliers;

public sealed class SupplierUpsertRequest
{
    [Required(ErrorMessage = "El nombre comercial es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre comercial no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de contacto es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre de contacto no puede superar los 150 caracteres.")]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(
        20,
        MinimumLength = 8,
        ErrorMessage = "El teléfono debe contener entre 8 y 20 caracteres.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(
        254,
        ErrorMessage = "El correo electrónico no puede superar los 254 caracteres.")]
    public string Email { get; set; } = string.Empty;
}