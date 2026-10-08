using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Shared.Expenses;

public sealed record BankVoucherMatchCandidateResponse(
    int ElectronicDocumentId,
    string IssuerName,
    string? IssuerCommercialName,
    DateTimeOffset IssueDate,
    decimal TotalAmount,
    string Currency,
    AccountingNature? AccountingNature,
    string? CategoryName);
