using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.BankVouchers;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class BankVoucherBatchRegressionTests
{
    [Fact]
    public async Task GetAsync_RequeuesOnlyVoucherWithCompatibleMerchantAndAmount()
    {
        await using var db = CreateDb();
        var matching = CreateVoucher("IKAMI HOME", 7243.47m, "CRC");
        var differentMerchant = CreateVoucher("DIFFERENT STORE", 7243.47m, "CRC");
        var differentAmount = CreateVoucher("IKAMI HOME", 8000m, "CRC");
        var differentCurrency = CreateVoucher("IKAMI HOME", 7243.47m, "USD");

        db.BankVouchers.AddRange(
            matching, differentMerchant, differentAmount, differentCurrency);
        db.ElectronicDocuments.Add(CreateDocument());
        await db.SaveChangesAsync();

        var page = await new BankVoucherReviewService(db).GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.NeedsReview,
                PageSize = 10
            },
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(matching.Id, item.Id);
        Assert.Single(item.MatchCandidates);
        Assert.Equal(BankVoucherStatus.NeedsReview, matching.Status);
        Assert.Equal(BankVoucherStatus.Accepted, differentMerchant.Status);
        Assert.Equal(BankVoucherStatus.Accepted, differentAmount.Status);
        Assert.Equal(BankVoucherStatus.Accepted, differentCurrency.Status);
    }

    [Fact]
    public async Task GetAsync_MultipleBatchesPreserveAcceptedPagination()
    {
        await using var db = CreateDb();
        for (var index = 0; index < 31; index++)
        {
            var voucher = CreateVoucher("IKAMI HOME", 7243.47m, "CRC");
            voucher.TransactionDate = voucher.TransactionDate.AddSeconds(index);
            db.BankVouchers.Add(voucher);
        }

        await db.SaveChangesAsync();

        var page = await new BankVoucherReviewService(db).GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.Accepted,
                Page = 2,
                PageSize = 10
            },
            CancellationToken.None);

        Assert.Equal(31, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
        Assert.All(page.Items,
            item => Assert.Equal(BankVoucherStatus.Accepted, item.Status));
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"BankVoucherBatchRegression-{Guid.NewGuid():N}")
            .Options);

    private static BankVoucher CreateVoucher(
        string merchantName, decimal amount, string currency) => new()
    {
        Bank = "Banco Nacional",
        MerchantName = merchantName,
        Amount = amount,
        Currency = currency,
        TransactionDate = new DateTimeOffset(
            2026, 9, 18, 11, 29, 0, TimeSpan.FromHours(-6)),
        GmailMessageId = Guid.NewGuid().ToString("N"),
        Status = BankVoucherStatus.Accepted,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static ElectronicDocument CreateDocument() => new()
    {
        Clave = Guid.NewGuid().ToString("N").PadRight(50, '0'),
        DocumentType = ElectronicDocumentType.Invoice,
        NumeroConsecutivo = "00100001010000000001",
        FechaEmision = new DateTimeOffset(
            2026, 9, 19, 10, 0, 0, TimeSpan.FromHours(-6)),
        IssuerEconomicActivityCode = "000000",
        ReceiverEconomicActivityCode = "000000",
        IssuerName = "IKAMI HOME",
        IssuerCommercialName = "IKAMI HOME",
        IssuerIdentificationType = "02",
        IssuerIdentification = "3101000000",
        ReceiverIdentificationType = "02",
        ReceiverIdentification = "3101000000",
        ReceiverName = "Revestik",
        SaleConditionCode = "01",
        CurrencyCode = "CRC",
        ExchangeRate = 1m,
        TotalDocument = 7243.47m,
        ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
        OriginalXml = [1],
        ImportedByUserId = "test-user",
        ImportedAtUtc = DateTime.UtcNow
    };
}
