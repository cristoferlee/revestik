namespace Revestik.Shared.ElectronicDocuments;

public sealed record ElectronicDocumentCategoryResponse(
    int Id,
    string Name,
    AccountingNature AccountingNature,
    int SortOrder,
    bool IsSystemDefault,
    bool AllowsAutomaticSuggestion,
    bool IsActive,
    string? SystemKey = null,
    OperationalDestination? DefaultOperationalDestination = null);
