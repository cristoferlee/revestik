using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Models;

public sealed class ElectronicDocumentCabysClassificationRule
{
    public int Id { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public string IssuerIdentificationType { get; set; } = string.Empty;
    public string IssuerIdentification { get; set; } = string.Empty;
    public string? CabysCode { get; set; }
    public string? CabysCategory4Code { get; set; }
    public int CategoryId { get; set; }
    public ElectronicDocumentCategory Category { get; set; } = null!;
    public OperationalDestination OperationalDestination { get; set; }
    public int ConfirmationCount { get; set; } = 1;
    public int? LastConfirmedElectronicDocumentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
