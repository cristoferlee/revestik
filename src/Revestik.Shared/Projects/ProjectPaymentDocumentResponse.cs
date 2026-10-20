namespace Revestik.Shared.Projects;

// Only metadata for documents currently attached to a specific project payment.
public sealed record ProjectPaymentDocumentResponse(int Id, string DisplayName, string Category);
