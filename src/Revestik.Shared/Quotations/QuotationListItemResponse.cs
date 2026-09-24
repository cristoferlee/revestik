namespace Revestik.Shared.Quotations;

public sealed class QuotationListItemResponse
{
    public int Id { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerIdentificationNumber { get; set; } = string.Empty;

    public Currency Currency { get; set; }

    public decimal Total { get; set; }

    public QuotationStatus Status { get; set; }

    public int? ConvertedSaleId { get; set; }

    public string ConvertedSaleNumber { get; set; } = string.Empty;
}