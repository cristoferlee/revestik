using Revestik.Shared.Quotations;
namespace Revestik.Api.Models;

public sealed class Quotation
{
    public int Id { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<QuotationLine> Lines { get; set; } = [];
    public ICollection<QuotationCharge> Charges { get; set; } = [];
    public Currency Currency { get; set; } = Currency.CRC;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}