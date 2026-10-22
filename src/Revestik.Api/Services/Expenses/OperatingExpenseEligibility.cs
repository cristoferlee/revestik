using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.Expenses;

// Pure eligibility rule: never classify a payment as an economic expense.
public static class OperatingExpenseEligibility
{
    public static bool IsEligible(AccountingNature nature, OperationalDestination? destination) =>
        nature == AccountingNature.OperatingExpense &&
        destination == OperationalDestination.InternalExpense;
}
