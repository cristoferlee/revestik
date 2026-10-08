using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class BankVoucherModelTests
{
    [Fact]
    public void BankVoucher_DefaultStatus_IsNeedsReview()
    {
        var voucher = new BankVoucher();

        Assert.Equal(BankVoucherStatus.NeedsReview, voucher.Status);
    }

    [Fact]
    public void Model_ConfiguresUniqueGmailMessageId_AndRestrictDocumentRelation()
    {
        using var dbContext = CreateDb();
        var entityType = dbContext.Model.FindEntityType(typeof(BankVoucher));

        Assert.NotNull(entityType);

        var gmailMessageId = entityType.FindProperty(nameof(BankVoucher.GmailMessageId));
        Assert.NotNull(gmailMessageId);
        Assert.False(gmailMessageId.IsNullable);
        Assert.Equal(255, gmailMessageId.GetMaxLength());

        var gmailIndex = entityType
            .GetIndexes()
            .Single(index =>
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(BankVoucher.GmailMessageId));

        Assert.True(gmailIndex.IsUnique);

        var relation = entityType
            .GetForeignKeys()
            .Single(foreignKey =>
                foreignKey.Properties.Count == 1 &&
                foreignKey.Properties[0].Name ==
                    nameof(BankVoucher.MatchedElectronicDocumentId));

        Assert.Equal(DeleteBehavior.Restrict, relation.DeleteBehavior);
        Assert.Equal(typeof(ElectronicDocument), relation.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Model_ConfiguresStatusAndTransactionDateCompositeIndex()
    {
        using var dbContext = CreateDb();
        var entityType = dbContext.Model.FindEntityType(typeof(BankVoucher));

        Assert.NotNull(entityType);

        var index = entityType
            .GetIndexes()
            .Single(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(BankVoucher.Status),
                        nameof(BankVoucher.TransactionDate)
                    ]));

        Assert.False(index.IsUnique);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"BankVoucherModelTests-{Guid.NewGuid()}")
            .Options);
}
