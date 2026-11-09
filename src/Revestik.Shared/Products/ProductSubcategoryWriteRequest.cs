using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Products;

public sealed class ProductSubcategoryWriteRequest : ProductSubcategoryUpsertRequest
{
    [Required]
    public string Version { get; set; } = string.Empty;
}
