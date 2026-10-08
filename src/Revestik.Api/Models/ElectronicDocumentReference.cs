namespace Revestik.Api.Models;

public sealed class ElectronicDocumentReference
{
    public int Id { get; set; }
    public int ElectronicDocumentId { get; set; }
    public ElectronicDocument ElectronicDocument { get; set; } = null!;

    public int Sequence { get; set; }
    public string ReferencedDocumentTypeCode { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTimeOffset ReferencedIssueDate { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    // The referenced document may not have been imported yet.
    public int? RelatedElectronicDocumentId { get; set; }
    public ElectronicDocument? RelatedElectronicDocument { get; set; }
}
