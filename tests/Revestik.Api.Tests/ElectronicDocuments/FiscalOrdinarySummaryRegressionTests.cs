using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.Expenses;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalOrdinarySummaryRegressionTests
{
    [Fact]
    public async Task PeriodSummary_CountsAllFiscalEvidence_ButExcludesAdjustmentsAndPaymentReceiptsFromTotals()
    {
        await using var db = CreateDb();
        AddDocuments(db);
        await db.SaveChangesAsync();

        var result = await new ElectronicDocumentPeriodSummaryService(db).GetSummaryAsync(
            new ElectronicDocumentSummaryRequest(), CancellationToken.None);

        Assert.Equal(5, result.ProcessedCount);
        var total = Assert.Single(result.AccountingTotals!);
        Assert.Equal(100m, total.ExpensesTotal);
        var financial = Assert.Single(result.FinancialTotals!);
        Assert.Equal(100m, financial.CashTotal);
    }

    [Fact]
    public async Task ExpenseSummary_ExcludesAdjustmentsIssuedDocumentsAndPaymentReceipts()
    {
        await using var db = CreateDb();
        AddDocuments(db);
        await db.SaveChangesAsync();

        var result = await new ExpenseReportingService(db).GetConsolidatedSummaryAsync(
            new ExpenseSummaryRequest(), CancellationToken.None);

        var total = Assert.Single(result.Totals);
        Assert.Equal("CRC", total.Currency);
        Assert.Equal(100m, total.ElectronicDocumentExpenses);
        Assert.Equal(100m, total.Total);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalOrdinarySummary-{Guid.NewGuid()}").Options);

    private static void AddDocuments(RevestikDbContext db)
    {
        var category = new ElectronicDocumentCategory
        {
            Name = "Gasto operativo",
            AccountingNature = AccountingNature.OperatingExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.ElectronicDocuments.AddRange(
            Document(1, ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received, 100m, category),
            Document(2, ElectronicDocumentType.CreditNote, ElectronicDocumentDirection.Received, 20m, category),
            Document(3, ElectronicDocumentType.DebitNote, ElectronicDocumentDirection.Received, 30m, category),
            Document(4, ElectronicDocumentType.ElectronicPaymentReceipt, ElectronicDocumentDirection.Received, 40m, category),
            Document(5, ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Issued, 50m, category));
    }

    private static ElectronicDocument Document(
        int id, ElectronicDocumentType type, ElectronicDocumentDirection direction,
        decimal amount, ElectronicDocumentCategory category) => new()
    {
        Clave = id.ToString().PadLeft(50, '0'),
        DocumentType = type,
        Direction = direction,
        NumeroConsecutivo = id.ToString().PadLeft(20, '0'),
        FechaEmision = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(-6)),
        IssuerName = "Emisor", IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
        ReceiverName = "Revestik", ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
        SaleConditionCode = "01", CurrencyCode = "CRC", ExchangeRate = 1m,
        TotalDocument = amount, TotalNetSale = amount, TotalSale = amount,
        ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
        Category = category, OriginalXml = [1], ImportedByUserId = "test-user",
        ImportedAtUtc = DateTime.UtcNow
    };
}
