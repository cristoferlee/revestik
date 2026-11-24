using Revestik.Api.Models.Identity;

namespace Revestik.Api.Models.PublicCatalog;

// Public lead-generation selection only: never an inventory reservation or sale.
public sealed class PublicCartItem
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int ProductId { get; set; }
    public PublicCatalogProduct Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
