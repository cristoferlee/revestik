using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Customers;

public sealed class CustomerUpsertRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(
        50,
        ErrorMessage = "La identificación no puede superar los 50 caracteres.")]
    public string? IdentificationNumber { get; set; }

    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(
        254,
        ErrorMessage = "El correo electrónico no puede superar los 254 caracteres.")]
    public string? Email { get; set; }

    [StringLength(
        25,
        ErrorMessage = "El teléfono no puede superar los 25 caracteres.")]
    public string? PhoneNumber { get; set; }

    [StringLength(
        500,
        ErrorMessage = "La dirección no puede superar los 500 caracteres.")]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}