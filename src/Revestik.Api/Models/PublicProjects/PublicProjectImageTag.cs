using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Models.PublicProjects;

// A tag belongs to a public photograph and only references the *public* catalog.
// ProductId can become null after a hard delete; snapshots keep unavailable-item links useful.
public sealed class PublicProjectImageTag
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public PublicProjectImage Image { get; set; } = null!;
    public int? ProductId { get; set; }
    public PublicCatalogProduct? Product { get; set; }

    // Coordinates in [0,1], relative to the original image dimensions.
    // Store the marker and bubble separately so the connector can be rendered responsively.
    public decimal AnchorX { get; set; }
    public decimal AnchorY { get; set; }
    public decimal LabelX { get; set; }
    public decimal LabelY { get; set; }
    public int DisplayOrder { get; set; }

    // Independent short text overrides; missing languages fall back to es.
    public string? CustomLabelEs { get; set; }
    public string? CustomLabelEn { get; set; }
    public string? CustomLabelDe { get; set; }
    // The source product may be renamed/unpublished/deleted in 01B; keep last known names.
    public string ProductNameSnapshotEs { get; set; } = string.Empty;
    public string? ProductNameSnapshotEn { get; set; }
    public string? ProductNameSnapshotDe { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
