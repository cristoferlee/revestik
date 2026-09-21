using Revestik.Api.Models.Identity;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Models;

public sealed class Quotation
{
    public int Id { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string CustomerNameSnapshot { get; set; } = string.Empty;

    public string CustomerIdentificationNumberSnapshot { get; set; } = string.Empty;

    public string CustomerEmailSnapshot { get; set; } = string.Empty;

    public string CustomerPhoneNumberSnapshot { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<QuotationLine> Lines { get; set; } = [];

    public ICollection<QuotationCharge> Charges { get; set; } = [];

    public Currency Currency { get; set; } = Currency.CRC;

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public string Observations { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
