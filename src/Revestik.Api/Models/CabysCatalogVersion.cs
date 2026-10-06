namespace Revestik.Api.Models;

public sealed class CabysCatalogVersion
{
    public string Version { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public int ItemCount { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }

    public ICollection<CabysItem> Items { get; set; } = [];
}
