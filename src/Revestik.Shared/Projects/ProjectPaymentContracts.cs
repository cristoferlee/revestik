namespace Revestik.Shared.Projects;

public sealed record ProjectPaymentUpsertRequest(decimal Amount, string CurrencyCode,
    DateOnly PaidOn, string? Reference, string? Notes, long? Version = null,
    string? Reason = null);
public sealed record ProjectPaymentVoidRequest(long Version, string Reason);
public sealed record ProjectAgreedAmountChangeRequest(decimal? Amount, string? CurrencyCode,
    long Version, string Reason);
public sealed record ProjectPaymentResponse(int Id, int ProjectId, decimal Amount,
    string CurrencyCode, DateOnly PaidOn, string Reference, string Notes, string Status,
    long Version, string CreatedByUserId, DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc, string? VoidReason, bool HasEvidence);
public sealed record ProjectBalanceResponse(int ProjectId, decimal? AgreedAmount,
    string? CurrencyCode, decimal PaidAmount, decimal? RemainingBalance);
public sealed record ProjectPaymentRevisionResponse(long Id, long PreviousVersion,
    decimal PreviousAmount, string PreviousCurrencyCode, DateOnly PreviousPaidOn,
    string PreviousReference, string PreviousNotes, string Reason,
    string ChangedByUserId, DateTime ChangedAtUtc);
public sealed record ProjectAgreedAmountRevisionResponse(long Id, long PreviousProjectVersion,
    decimal? PreviousAmount, decimal? NewAmount, string? PreviousCurrencyCode,
    string? NewCurrencyCode, string Reason, string ChangedByUserId, DateTime ChangedAtUtc);
