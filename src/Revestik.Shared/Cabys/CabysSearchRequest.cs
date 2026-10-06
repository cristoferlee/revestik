using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Cabys;

public sealed class CabysSearchRequest
{
    [StringLength(150)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
