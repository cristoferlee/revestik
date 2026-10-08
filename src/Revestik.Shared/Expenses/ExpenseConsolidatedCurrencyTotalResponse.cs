namespace Revestik.Shared.Expenses;

public sealed record ExpenseConsolidatedCurrencyTotalResponse(
    string Currency,
    decimal ManualExpenses,
    decimal ElectronicDocumentExpenses,
    decimal AcceptedBankVouchers,
    decimal Total);
