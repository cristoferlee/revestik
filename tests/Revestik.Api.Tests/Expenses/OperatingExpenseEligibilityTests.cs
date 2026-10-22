using Revestik.Api.Services.Expenses;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.Expenses;

public sealed class OperatingExpenseEligibilityTests
{
    [Theory]
    [InlineData(OperationalDestination.InventoryWarehouse)]
    [InlineData(OperationalDestination.DirectCustomer)]
    [InlineData(OperationalDestination.Asset)]
    [InlineData(OperationalDestination.NoAction)]
    public void Excludes_NonExpenseDestinations(OperationalDestination destination)
    {
        Assert.False(OperatingExpenseEligibility.IsEligible(AccountingNature.OperatingExpense, destination));
    }

    [Fact]
    public void Requires_OperatingNature_And_InternalExpenseDestination()
    {
        Assert.True(OperatingExpenseEligibility.IsEligible(AccountingNature.OperatingExpense,
            OperationalDestination.InternalExpense));
        Assert.False(OperatingExpenseEligibility.IsEligible(AccountingNature.Inventory,
            OperationalDestination.InternalExpense));
        Assert.False(OperatingExpenseEligibility.IsEligible(AccountingNature.OperatingExpense, null));
    }
}
