namespace Revestik.Shared.Sales;

public sealed class SaleListItemResponse
{
    public int Id { get; set; }

    public string SaleNumber { get; set; } = string.Empty;

    public string SourceQuotationNumber { get; set; } = string.Empty;

    public DateTime? IssuedAtUtc { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public Currency Currency { get; set; }

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingAmount { get; set; }

    public SaleStatus Status { get; set; }

    public SaleBalanceStatus BalanceStatus { get; set; }
}