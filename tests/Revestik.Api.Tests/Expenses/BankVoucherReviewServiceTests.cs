using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.BankVouchers;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class BankVoucherReviewServiceTests
{
    [Fact]
    public async Task GetAsync_FindsCompatibleElectronicDocumentCandidate()
    {
        await using var db = CreateDb();

        var category = CreateExpenseCategory();
        db.ElectronicDocumentCategories.Add(category);

        var document = CreateDocument(
            "IKAMI HOME",
            7243.47m,
            new DateTimeOffset(
                2026, 9, 19, 10, 0, 0,
                TimeSpan.FromHours(-6)),
            category);

        db.ElectronicDocuments.Add(document);
        db.BankVouchers.Add(
            CreateVoucher(
                BankVoucherStatus.NeedsReview));

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var page = await service.GetAsync(
            new BankVoucherListRequest(),
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        var candidate = Assert.Single(item.MatchCandidates);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(document.Id, candidate.ElectronicDocumentId);
        Assert.Equal(
            AccountingNature.OperatingExpense,
            candidate.AccountingNature);
    }

    [Fact]
    public async Task GetAsync_AcceptedVoucherWithLateCandidate_ReturnsToNeedsReview()
    {
        await using var db = CreateDb();

        db.ElectronicDocuments.Add(
            CreateDocument(
                "IKAMI HOME",
                7243.47m,
                new DateTimeOffset(
                    2026, 9, 20, 8, 0, 0,
                    TimeSpan.FromHours(-6)),
                null));

        db.BankVouchers.Add(
            CreateVoucher(
                BankVoucherStatus.Accepted));

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var page = await service.GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.NeedsReview
            },
            CancellationToken.None);

        var item = Assert.Single(page.Items);

        Assert.Equal(
            BankVoucherStatus.NeedsReview,
            item.Status);

        Assert.Equal(
            BankVoucherStatus.NeedsReview,
            Assert.Single(db.BankVouchers).Status);
    }

    [Fact]
    public async Task GetAsync_StatusFilter_ReturnsOnlyRequestedStatus()
    {
        await using var db = CreateDb();

        db.BankVouchers.AddRange(
            CreateVoucher(BankVoucherStatus.NeedsReview),
            CreateVoucher(BankVoucherStatus.Accepted),
            CreateVoucher(BankVoucherStatus.Ignored));

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var page = await service.GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.NeedsReview
            },
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(BankVoucherStatus.NeedsReview, item.Status);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetAsync_History_ExcludesNeedsReview()
    {
        await using var db = CreateDb();

        db.BankVouchers.AddRange(
            CreateVoucher(BankVoucherStatus.NeedsReview),
            CreateVoucher(BankVoucherStatus.Accepted),
            CreateVoucher(BankVoucherStatus.Matched),
            CreateVoucher(BankVoucherStatus.Ignored));

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var page = await service.GetAsync(
            new BankVoucherListRequest
            {
                ExcludeNeedsReview = true
            },
            CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.All(
            page.Items,
            item => Assert.NotEqual(
                BankVoucherStatus.NeedsReview,
                item.Status));
    }

    [Fact]
    public async Task GetAsync_PaginatesTenItems()
    {
        await using var db = CreateDb();

        for (var index = 0; index < 12; index++)
        {
            var voucher = CreateVoucher(
                BankVoucherStatus.NeedsReview);

            voucher.TransactionDate = voucher.TransactionDate
                .AddMinutes(index);

            db.BankVouchers.Add(voucher);
        }

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var firstPage = await service.GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.NeedsReview,
                Page = 1,
                PageSize = 10
            },
            CancellationToken.None);

        var secondPage = await service.GetAsync(
            new BankVoucherListRequest
            {
                Status = BankVoucherStatus.NeedsReview,
                Page = 2,
                PageSize = 10
            },
            CancellationToken.None);

        Assert.Equal(12, firstPage.TotalCount);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(2, secondPage.Items.Count);
    }

    [Fact]
    public async Task MatchAsync_ValidCandidate_LinksAndMarksMatched()
    {
        await using var db = CreateDb();

        var document = CreateDocument(
            "IKAMI HOME",
            7243.47m,
            new DateTimeOffset(
                2026, 9, 18, 12, 0, 0,
                TimeSpan.FromHours(-6)),
            null);

        var voucher = CreateVoucher(
            BankVoucherStatus.NeedsReview);

        db.AddRange(document, voucher);
        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var matched = await service.MatchAsync(
            voucher.Id,
            document.Id,
            CancellationToken.None);

        Assert.True(matched);
        Assert.Equal(
            BankVoucherStatus.Matched,
            voucher.Status);
        Assert.Equal(
            document.Id,
            voucher.MatchedElectronicDocumentId);
    }

    [Fact]
    public async Task MatchAsync_IncompatibleDocument_DoesNotLink()
    {
        await using var db = CreateDb();

        var document = CreateDocument(
            "OTRO COMERCIO",
            7243.47m,
            new DateTimeOffset(
                2026, 9, 18, 12, 0, 0,
                TimeSpan.FromHours(-6)),
            null);

        var voucher = CreateVoucher(
            BankVoucherStatus.NeedsReview);

        db.AddRange(document, voucher);
        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var matched = await service.MatchAsync(
            voucher.Id,
            document.Id,
            CancellationToken.None);

        Assert.False(matched);
        Assert.Equal(
            BankVoucherStatus.NeedsReview,
            voucher.Status);
        Assert.Null(voucher.MatchedElectronicDocumentId);
    }

    [Fact]
    public async Task GetAsync_KnownSupplierWithoutInvoice_MarksMerchantKnown()
    {
        await using var db = CreateDb();

        db.Suppliers.Add(
            new Supplier
            {
                Name = "Ikami Home",
                CommercialName = "Ikami Home",
                ContactName = string.Empty,
                PhoneNumber = string.Empty,
                Email = string.Empty,
                Address = string.Empty,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        db.BankVouchers.Add(
            CreateVoucher(
                BankVoucherStatus.NeedsReview));

        await db.SaveChangesAsync();

        var service = new BankVoucherReviewService(db);

        var page = await service.GetAsync(
            new BankVoucherListRequest(),
            CancellationToken.None);

        var item = Assert.Single(page.Items);

        Assert.True(item.IsKnownMerchant);
        Assert.Empty(item.MatchCandidates);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"BankVoucherReviewTests-{Guid.NewGuid()}")
            .Options);

    private static BankVoucher CreateVoucher(
        BankVoucherStatus status) =>
        new()
        {
            Bank = "Banco Nacional",
            MerchantName = "IKAMI HOME",
            Amount = 7243.47m,
            Currency = "CRC",
            TransactionDate = new DateTimeOffset(
                2026, 9, 18, 11, 29, 0,
                TimeSpan.FromHours(-6)),
            GmailMessageId = Guid.NewGuid().ToString("N"),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static ElectronicDocument CreateDocument(
        string issuerName,
        decimal total,
        DateTimeOffset date,
        ElectronicDocumentCategory? category) =>
        new()
        {
            Clave = Guid.NewGuid().ToString("N")[..32]
                .PadRight(50, '0'),
            DocumentType =
                ElectronicDocumentType.Invoice,
            NumeroConsecutivo = "00100001010000000001",
            FechaEmision = date,
            IssuerEconomicActivityCode = "000000",
            ReceiverEconomicActivityCode = "000000",
            IssuerName = issuerName,
            IssuerCommercialName = issuerName,
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101000000",
            IssuerPhoneNumber = string.Empty,
            IssuerEmail = string.Empty,
            IssuerAddress = string.Empty,
            ReceiverName = "Revestik",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3101000000",
            SaleConditionCode = "01",
            CurrencyCode = "CRC",
            ExchangeRate = 1m,
            TotalDocument = total,
            ProcessingStatus =
                ElectronicDocumentProcessingStatus.Processed,
            Category = category,
            OriginalXml = [1],
            ImportedByUserId = "test-user",
            ImportedAtUtc = DateTime.UtcNow
        };

    private static ElectronicDocumentCategory CreateExpenseCategory() =>
        new()
        {
            Name = "Gasto test",
            AccountingNature =
                AccountingNature.OperatingExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
}
