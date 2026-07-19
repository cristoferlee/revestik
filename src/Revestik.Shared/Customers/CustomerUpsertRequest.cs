using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Customers;

public sealed class CustomerUpsertRequest : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de identificación es obligatorio.")]
    public IdentificationType? IdentificationType { get; set; }

    [Required(ErrorMessage = "La identificación es obligatoria.")]
    [StringLength(
        12,
        ErrorMessage = "La identificación no puede superar los 12 dígitos.")]
    public string IdentificationNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(
        254,
        ErrorMessage = "El correo electrónico no puede superar los 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(
        20,
        MinimumLength = 8,
        ErrorMessage = "El teléfono debe contener entre 8 y 20 caracteres.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(
        500,
        ErrorMessage = "La dirección no puede superar los 500 caracteres.")]
    public string Address { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(IdentificationNumber))
        {
            yield break;
        }

        var normalizedIdentification = IdentificationNumber.Trim();

        if (!normalizedIdentification.All(char.IsDigit))
        {
            yield return new ValidationResult(
                "La identificación debe contener únicamente dígitos, sin guiones ni espacios.",
                [nameof(IdentificationNumber)]);

            yield break;
        }

        switch (IdentificationType)
        {
            case Customers.IdentificationType.PhysicalPerson:
                if (normalizedIdentification.Length != 9 ||
                    normalizedIdentification.StartsWith('0'))
                {
                    yield return new ValidationResult(
                        "La cédula física debe contener 9 dígitos y no puede iniciar con cero.",
                        [nameof(IdentificationNumber)]);
                }

                break;

            case Customers.IdentificationType.LegalEntity:
                if (normalizedIdentification.Length != 10)
                {
                    yield return new ValidationResult(
                        "La cédula jurídica debe contener 10 dígitos.",
                        [nameof(IdentificationNumber)]);
                }

                break;

            case Customers.IdentificationType.Dimex:
                if ((normalizedIdentification.Length != 11 &&
                     normalizedIdentification.Length != 12) ||
                    normalizedIdentification.StartsWith('0'))
                {
                    yield return new ValidationResult(
                        "El DIMEX debe contener 11 o 12 dígitos y no puede iniciar con cero.",
                        [nameof(IdentificationNumber)]);
                }

                break;
        }
    }
}