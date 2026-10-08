using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Expenses;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class ExpenseReportingServiceTests
{
    [Fact]
    public async Task ConsolidatedSummary_AddsManualXmlExpenseAndAcceptedVoucher()
    {
        await using var db = CreateDb();

        db.Expenses.Add(new Expense
        {
            Name = "Planilla",
            Description = "Manual",
            TotalAmount = 100m,
            ExpenseDate = new DateOnly(2026, 10, 7),
            CreatedAtUtc = DateTime.UtcNow
        });

        var category = new ElectronicDocumentCategory
        {
            Name = "Gasto",
            AccountingNature = AccountingNature.OperatingExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.ElectronicDocuments.Add(
            CreateDocument(category, 200m, "CRC"));

        db.BankVouchers.AddRange(
            CreateVoucher(BankVoucherStatus.Accepted, 300m, "CRC"),
            CreateVoucher(BankVoucherStatus.NeedsReview, 400m, "CRC"),
            CreateVoucher(BankVoucherStatus.Matched, 500m, "CRC"),
            CreateVoucher(BankVoucherStatus.Ignored, 600m, "CRC"));

        await db.SaveChangesAsync();

        var service = new ExpenseReportingService(db);

        var result = await service.GetConsolidatedSummaryAsync(
            new ExpenseSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 31)
            },
            CancellationToken.None);

        var crc = Assert.Single(result.Totals);
        Assert.Equal("CRC", crc.Currency);
        Assert.Equal(100m, crc.ManualExpenses);
        Assert.Equal(200m, crc.ElectronicDocumentExpenses);
        Assert.Equal(300m, crc.AcceptedBankVouchers);
        Assert.Equal(600m, crc.Total);

        Assert.Equal(1, result.AcceptedVoucherCount);
        Assert.Equal(1, result.NeedsReviewVoucherCount);
        Assert.Equal(1, result.MatchedVoucherCount);
        Assert.Equal(1, result.IgnoredVoucherCount);
    }

    [Fact]
    public async Task ConsolidatedSummary_KeepsCurrenciesSeparate()
    {
        await using var db = CreateDb();

        db.BankVouchers.AddRange(
            CreateVoucher(BankVoucherStatus.Accepted, 25m, "USD"),
            CreateVoucher(BankVoucherStatus.Accepted, 1000m, "CRC"));

        await db.SaveChangesAsync();

        var result = await new ExpenseReportingService(db)
            .GetConsolidatedSummaryAsync(
                new ExpenseSummaryRequest(),
                CancellationToken.None);

        Assert.Equal(2, result.Totals.Count);
        Assert.Contains(result.Totals, x =>
            x.Currency == "CRC" && x.Total == 1000m);
        Assert.Contains(result.Totals, x =>
            x.Currency == "USD" && x.Total == 25m);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"ExpenseReportingTests-{Guid.NewGuid()}")
            .Options);

    private static BankVoucher CreateVoucher(
        BankVoucherStatus status,
        decimal amount,
        string currency) =>
        new()
        {
            Bank = "Banco Nacional",
            MerchantName = "Comercio",
            Amount = amount,
            Currency = currency,
            TransactionDate = new DateTimeOffset(
                2026, 10, 7, 10, 0, 0,
                TimeSpan.FromHours(-6)),
            GmailMessageId = Guid.NewGuid().ToString("N"),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static ElectronicDocument CreateDocument(
        ElectronicDocumentCategory category,
        decimal total,
        string currency) =>
        new()
        {
            Clave = Guid.NewGuid().ToString("N")[..32].PadRight(50, '0'),
            DocumentType = ElectronicDocumentType.Invoice,
            NumeroConsecutivo = "00100001010000000001",
            FechaEmision = new DateTimeOffset(
                2026, 10, 7, 8, 0, 0,
                TimeSpan.FromHours(-6)),
            IssuerEconomicActivityCode = "000000",
            ReceiverEconomicActivityCode = "000000",
            IssuerName = "Proveedor",
            IssuerCommercialName = "Proveedor",
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101000000",
            IssuerPhoneNumber = string.Empty,
            IssuerEmail = string.Empty,
            IssuerAddress = string.Empty,
            ReceiverName = "Revestik",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3101000000",
            SaleConditionCode = "01",
            CurrencyCode = currency,
            ExchangeRate = 1m,
            TotalDocument = total,
            ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
            Category = category,
            OriginalXml = [1],
            ImportedByUserId = "test-user",
            ImportedAtUtc = DateTime.UtcNow
        };
}
