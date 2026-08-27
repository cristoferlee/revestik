using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Customers;

public sealed class CustomerListRequest
{
    [StringLength(
        150,
        ErrorMessage = "La búsqueda no puede superar los 150 caracteres.")]
    public string? Search { get; set; }

    [EnumDataType(
    typeof(IdentificationType),
    ErrorMessage = "El tipo de identificación no es válido.")]
    public IdentificationType? IdentificationType { get; set; }

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "El número de página debe ser mayor que cero.")]
    public int Page { get; set; } = 1;

    [Range(
        1,
        100,
        ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; set; } = 20;
}