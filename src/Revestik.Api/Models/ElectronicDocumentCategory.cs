using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Models;

public sealed class ElectronicDocumentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SystemKey { get; set; }
    public AccountingNature AccountingNature { get; set; } = AccountingNature.Other;
    public OperationalDestination? DefaultOperationalDestination { get; set; }
    public bool IsDefaultDestinationInitialized { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystemDefault { get; set; }
    public bool AllowsAutomaticSuggestion { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
