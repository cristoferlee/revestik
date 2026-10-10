using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentPeriodSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_FiltersCountsAndTotalsByFechaEmision()
    {
        await using var dbContext = CreateDbContext();

        var category = new ElectronicDocumentCategory
        {
            Name = "Inventario test",
            AccountingNature = AccountingNature.Inventory,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ElectronicDocumentCategories.Add(category);
        await dbContext.SaveChangesAsync();

        dbContext.ElectronicDocuments.AddRange(
            CreateDocument(
                1,
                "50601102600310115777600500001010000051684120639721",
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                ElectronicDocumentProcessingStatus.Processed,
                100_000m,
                category.Id),
            CreateDocument(
                2,
                "50631102600310115777600500001010000051684120639722",
                new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero),
                ElectronicDocumentProcessingStatus.Pending,
                20_000m,
                null),
            CreateDocument(
                3,
                "50630092600310115777600500001010000051684120639723",
                new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero),
                ElectronicDocumentProcessingStatus.Processed,
                999_999m,
                category.Id),
            CreateDocument(
                4,
                "50601112600310115777600500001010000051684120639724",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                ElectronicDocumentProcessingStatus.NoActionRequired,
                888_888m,
                null));
        await dbContext.SaveChangesAsync();

        var service = new ElectronicDocumentPeriodSummaryService(dbContext);
        var result = await service.GetSummaryAsync(
            new ElectronicDocumentSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 31)
            },
            CancellationToken.None);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.NoActionRequiredCount);

        var accounting = Assert.Single(result.AccountingTotals!);
        Assert.Equal("CRC", accounting.CurrencyCode);
        Assert.Equal(100_000m, accounting.PurchasesTotal);
        Assert.Equal(0m, accounting.ExpensesTotal);

        var financial = Assert.Single(result.FinancialTotals!);
        Assert.Equal(100_000m, financial.CashTotal);
        Assert.Equal(0m, financial.CreditTotal);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenPeriodHasNoDocuments_ReturnsZeroCountsAndEmptyTotals()
    {
        await using var dbContext = CreateDbContext();
        var service = new ElectronicDocumentPeriodSummaryService(dbContext);

        var result = await service.GetSummaryAsync(
            new ElectronicDocumentSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 31)
            },
            CancellationToken.None);

        Assert.Equal(0, result.PendingCount);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(0, result.NoActionRequiredCount);
        Assert.Equal(0, result.WithHaciendaResponseCount);
        Assert.Empty(result.AccountingTotals!);
        Assert.Empty(result.FinancialTotals!);
    }

    [Fact]
    public async Task GetSummaryAsync_UsesXmlCalendarDateAcrossOffsetsForCountsAndTotals()
    {
        await using var db = CreateDbContext();
        var category = new ElectronicDocumentCategory
        {
            Name = "Inventario", AccountingNature = AccountingNature.Inventory,
            IsActive = true, IsDefaultDestinationInitialized = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ElectronicDocumentCategories.Add(category);
        await db.SaveChangesAsync();

        var first = CreateDocument(21, "oct21", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(14)),
            ElectronicDocumentProcessingStatus.Processed, 100m, category.Id);
        var last = CreateDocument(22, "oct22", new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.FromHours(-14)),
            ElectronicDocumentProcessingStatus.Processed, 200m, category.Id);
        var previous = CreateDocument(23, "sep23", new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-14)),
            ElectronicDocumentProcessingStatus.Processed, 900m, category.Id);
        var next = CreateDocument(24, "nov24", new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(14)),
            ElectronicDocumentProcessingStatus.Processed, 800m, category.Id);
        db.ElectronicDocuments.AddRange(first, last, previous, next);
        await db.SaveChangesAsync();

        var result = await new ElectronicDocumentPeriodSummaryService(db).GetSummaryAsync(
            new ElectronicDocumentSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 1)
            }, CancellationToken.None);

        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(300m, Assert.Single(result.AccountingTotals!).PurchasesTotal);
        Assert.Equal(300m, Assert.Single(result.FinancialTotals!).CashTotal);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesNotesAndIssuedDocumentsFromEconomicTotalsButCountsThem()
    {
        await using var db = CreateDbContext();
        var category = new ElectronicDocumentCategory
        {
            Name = "Inventario", AccountingNature = AccountingNature.Inventory,
            IsActive = true, IsDefaultDestinationInitialized = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ElectronicDocumentCategories.Add(category);
        await db.SaveChangesAsync();
        ElectronicDocument Make(int id, ElectronicDocumentType type, ElectronicDocumentDirection direction, decimal amount)
        {
            var doc = CreateDocument(id, $"doc{id}", new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.FromHours(-6)),
                ElectronicDocumentProcessingStatus.Processed, amount, category.Id);
            doc.DocumentType = type;
            doc.Direction = direction;
            return doc;
        }
        db.ElectronicDocuments.AddRange(
            Make(31, ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received, 100m),
            Make(32, ElectronicDocumentType.CreditNote, ElectronicDocumentDirection.Received, 50m),
            Make(33, ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Issued, 500m));
        await db.SaveChangesAsync();

        var result = await new ElectronicDocumentPeriodSummaryService(db).GetSummaryAsync(
            new ElectronicDocumentSummaryRequest { DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 31) },
            CancellationToken.None);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(100m, Assert.Single(result.AccountingTotals!).PurchasesTotal);
        Assert.Equal(100m, Assert.Single(result.FinancialTotals!).CashTotal);
    }

    [Fact]
    public void SummaryRequest_WhenDateFromIsAfterDateTo_IsInvalid()
    {
        var request = new ElectronicDocumentSummaryRequest
        {
            DateFrom = new DateOnly(2026, 10, 31),
            DateTo = new DateOnly(2026, 10, 1)
        };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, x => x.ErrorMessage == "La fecha inicial no puede ser posterior a la fecha final.");
    }

    private static RevestikDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"ElectronicDocumentPeriodSummary-{Guid.NewGuid()}")
            .Options);

    private static ElectronicDocument CreateDocument(
        int id,
        string clave,
        DateTimeOffset fechaEmision,
        ElectronicDocumentProcessingStatus status,
        decimal total,
        int? categoryId) =>
        new()
        {
            Id = id,
            Clave = clave,
            DocumentType = ElectronicDocumentType.Invoice,
            Direction = ElectronicDocumentDirection.Received,
            NumeroConsecutivo = $"0010000101000005{id:0000}",
            FechaEmision = fechaEmision,
            IssuerEconomicActivityCode = string.Empty,
            ReceiverEconomicActivityCode = string.Empty,
            IssuerName = "Proveedor test",
            IssuerCommercialName = string.Empty,
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101157776",
            IssuerPhoneNumber = string.Empty,
            IssuerEmail = string.Empty,
            IssuerAddress = string.Empty,
            ReceiverName = "Revestik",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3102959852",
            SaleConditionCode = "01",
            CurrencyCode = "CRC",
            ExchangeRate = 1m,
            TotalSale = total,
            TotalNetSale = total,
            TotalTax = 0m,
            TotalDocument = total,
            ProcessingStatus = status,
            CategoryId = categoryId,
            OriginalXml = [1, 2, 3],
            ImportedByUserId = "period-summary-test-user",
            ImportedAtUtc = DateTime.UtcNow
        };
}
