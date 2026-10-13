namespace Revestik.Shared.ElectronicDocuments;

public sealed record FiscalDateExceptionRequest(int OriginalDocumentId, string Justification);

public sealed record FiscalReferenceResolutionResponse(
    int Id, int ReferenceId, int OriginalDocumentId,
    string ResolutionType, string Justification, string CompatibilityReasons,
    string ResolvedByUserId, DateTime ResolvedAtUtc);
