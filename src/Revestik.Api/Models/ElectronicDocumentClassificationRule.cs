namespace Revestik.Api.Models;

public sealed class ElectronicDocumentClassificationRule
{
    public int Id { get; set; }
    public string IssuerIdentificationType { get; set; } = string.Empty;
    public string IssuerIdentification { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public ElectronicDocumentCategory Category { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
