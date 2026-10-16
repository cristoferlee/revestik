namespace Revestik.Api.Models;

public sealed class ProjectPaymentDocumentLink
{
    public int ProjectId { get; set; }
    public int ProjectPaymentId { get; set; }
    public ProjectPayment Payment { get; set; } = null!;
    public int ProjectDocumentId { get; set; }
    public ProjectDocument Document { get; set; } = null!;
    public string LinkedByUserId { get; set; } = string.Empty;
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
}
