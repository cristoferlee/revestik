using Revestik.Shared.Projects;
namespace Revestik.Api.Models;
public sealed class ProjectLifecycleChange
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long PreviousProjectVersion { get; set; }
    public ProjectStatus PreviousStatus { get; set; }
    public ProjectStatus NewStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public string? ClosingObservations { get; set; }
    public int? EvidenceDocumentId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
