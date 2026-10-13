using Revestik.Api.Models.Identity;

namespace Revestik.Api.Models;

/// <summary>Immutable audit of a reviewed fiscal-reference discrepancy. Never edits the source XML or approves an adjustment.</summary>
public sealed class ElectronicDocumentReferenceResolution
{
    public int Id { get; set; }
    public int ElectronicDocumentReferenceId { get; set; }
    public ElectronicDocumentReference Reference { get; set; } = null!;
    public int OriginalDocumentId { get; set; }
    public ElectronicDocument OriginalDocument { get; set; } = null!;
    public string ResolutionType { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string CompatibilityReasons { get; set; } = string.Empty;
    public string ResolvedByUserId { get; set; } = string.Empty;
    public ApplicationUser ResolvedByUser { get; set; } = null!;
    public DateTime ResolvedAtUtc { get; set; }
}
