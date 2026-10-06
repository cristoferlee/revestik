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
