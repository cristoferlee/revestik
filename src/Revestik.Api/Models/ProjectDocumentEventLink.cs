namespace Revestik.Api.Models;

public sealed class ProjectDocumentEventLink
{
    public int ProjectId { get; set; }
    public int ProjectDocumentId { get; set; }
    public ProjectDocument Document { get; set; } = null!;
    public int ProjectEventId { get; set; }
    public ProjectEvent Event { get; set; } = null!;
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
    public string LinkedByUserId { get; set; } = string.Empty;
}
