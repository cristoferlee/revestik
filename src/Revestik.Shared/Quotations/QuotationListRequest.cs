using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Quotations;

public sealed class QuotationListRequest
{
    [StringLength(
        150,
        ErrorMessage = "La búsqueda no puede superar los 150 caracteres.")]
    public string? Search { get; set; }

    public DateTime? DateFromUtc { get; set; }

    public DateTime? DateToUtc { get; set; }

    [EnumDataType(
        typeof(QuotationStatus),
        ErrorMessage = "El estado de la cotización no es válido.")]
    public QuotationStatus? Status { get; set; }

    [EnumDataType(
        typeof(Currency),
        ErrorMessage = "La moneda no es válida.")]
    public Currency? Currency { get; set; }

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