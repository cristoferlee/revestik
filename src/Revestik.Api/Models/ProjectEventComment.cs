namespace Revestik.Api.Models;

// Comments are append-only: corrections are entered as a new comment.
public sealed class ProjectEventComment
{
    public long Id { get; set; }
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
