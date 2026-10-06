using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentServiceTests
{
    private const string UserId = "electronic-document-service-user";

    [Fact]
    public async Task ClassifyAsync_CreatesIssuerRuleAndProcessesDocument()
    {
        await using var dbContext = CreateDbContext();
        var category = await SeedAsync(dbContext);
        var service = new ElectronicDocumentService(dbContext);

        var result = await service.ClassifyAsync(
            1,
            new ElectronicDocumentClassifyRequest
            {
                CategoryId = category.Id,
                OperationalDestination = OperationalDestination.InventoryWarehouse,
                ProcessingStatus = ElectronicDocumentProcessingStatus.Processed
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal(ElectronicDocumentProcessingStatus.Processed, result.ProcessingStatus);
        Assert.NotNull(result.ProcessedAtUtc);
        Assert.Equal(OperationalDestination.InventoryWarehouse, result.OperationalDestination);

        var rule = await dbContext.ElectronicDocumentClassificationRules.SingleAsync();
        Assert.Equal("02", rule.IssuerIdentificationType);
        Assert.Equal("3101157776", rule.IssuerIdentification);
        Assert.Equal(category.Id, rule.CategoryId);
    }

    [Fact]
    public async Task ClassifyAsync_ReclassificationUpdatesExistingIssuerRule()
    {
        await using var dbContext = CreateDbContext();
        var firstCategory = await SeedAsync(dbContext);
        var secondCategory = new ElectronicDocumentCategory
        {
            Name = "Servicios",
            AccountingNature = AccountingNature.OperatingExpense,
            DefaultOperationalDestination = OperationalDestination.InternalExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ElectronicDocumentCategories.Add(secondCategory);
        await dbContext.SaveChangesAsync();

        var service = new ElectronicDocumentService(dbContext);

        await service.ClassifyAsync(
            1,
            new ElectronicDocumentClassifyRequest
            {
                CategoryId = firstCategory.Id,
                OperationalDestination = OperationalDestination.InventoryWarehouse,
                ProcessingStatus = ElectronicDocumentProcessingStatus.Processed
            },
            UserId,
            CancellationToken.None);

        await service.ClassifyAsync(
            1,
            new ElectronicDocumentClassifyRequest
            {
                CategoryId = secondCategory.Id,
                ProcessingStatus = ElectronicDocumentProcessingStatus.NoActionRequired
            },
            UserId,
            CancellationToken.None);

        var rules = await dbContext.ElectronicDocumentClassificationRules.ToListAsync();
        Assert.Single(rules);
        Assert.Equal(secondCategory.Id, rules[0].CategoryId);

        var document = await dbContext.ElectronicDocuments.SingleAsync();
        Assert.Equal(ElectronicDocumentProcessingStatus.NoActionRequired, document.ProcessingStatus);
    }

    [Fact]
    public async Task ClassifyAsync_WithLineOverrides_PersistsOverridesAndLearnsExactCabys()
    {
        await using var dbContext = CreateDbContext();
        var inventory = await SeedAsync(dbContext);
        var expense = new ElectronicDocumentCategory
        {
            Name = "Combustible test",
            AccountingNature = AccountingNature.OperatingExpense,
            DefaultOperationalDestination = OperationalDestination.InternalExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ElectronicDocumentCategories.Add(expense);
        dbContext.ElectronicDocumentLines.AddRange(
            CreateLine(1, 1, "3336001000200", 20_000m),
            CreateLine(2, 2, "1234567890123", 80_000m));
        await dbContext.SaveChangesAsync();

        var service = new ElectronicDocumentService(dbContext);
        var result = await service.ClassifyAsync(
            1,
            new ElectronicDocumentClassifyRequest
            {
                CategoryId = inventory.Id,
                OperationalDestination = OperationalDestination.InventoryWarehouse,
                ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
                SeparateByLine = true,
                LineClassifications =
                [
                    new ElectronicDocumentLineClassificationRequest
                    {
                        LineId = 1,
                        CategoryId = expense.Id,
                        OperationalDestination = OperationalDestination.InternalExpense
                    },
                    new ElectronicDocumentLineClassificationRequest
                    {
                        LineId = 2,
                        CategoryId = inventory.Id,
                        OperationalDestination = OperationalDestination.InventoryWarehouse
                    }
                ]
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.HasLineOverrides);
        Assert.Equal(expense.Id, result.Lines.Single(x => x.Id == 1).ClassificationCategoryId);
        Assert.Equal(OperationalDestination.InternalExpense, result.Lines.Single(x => x.Id == 1).OperationalDestination);

        var learned = await dbContext.ElectronicDocumentCabysClassificationRules
            .Where(x => x.CabysCode != null)
            .ToListAsync();
        Assert.Equal(2, learned.Count);
        Assert.Contains(learned, x => x.CabysCode == "3336001000200" && x.CategoryId == expense.Id);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenDocumentIsSplit_UsesLineTotalsByAccountingNature()
    {
        await using var dbContext = CreateDbContext();
        var inventory = await SeedAsync(dbContext);
        var expense = new ElectronicDocumentCategory
        {
            Name = "Gasto test",
            AccountingNature = AccountingNature.OperatingExpense,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var direct = new ElectronicDocumentCategory
        {
            Name = "Costo directo test",
            AccountingNature = AccountingNature.DirectCost,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ElectronicDocumentCategories.AddRange(expense, direct);
        await dbContext.SaveChangesAsync();

        var document = await dbContext.ElectronicDocuments.SingleAsync(x => x.Id == 1);
        document.CategoryId = inventory.Id;
        document.ProcessingStatus = ElectronicDocumentProcessingStatus.Processed;
        document.TotalDocument = 100_000m;
        dbContext.ElectronicDocumentLines.AddRange(
            CreateLine(1, 1, "1111111111111", 40_000m, expense.Id, OperationalDestination.InternalExpense),
            CreateLine(2, 2, "2222222222222", 60_000m, direct.Id, OperationalDestination.DirectCustomer));
        await dbContext.SaveChangesAsync();

        var service = new ElectronicDocumentService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        var crc = Assert.Single(summary.AccountingTotals!);
        Assert.Equal("CRC", crc.CurrencyCode);
        Assert.Equal(0m, crc.PurchasesTotal);
        Assert.Equal(40_000m, crc.ExpensesTotal);
        Assert.Equal(60_000m, crc.DirectCostsTotal);
        Assert.Equal(0m, crc.UnallocatedTotal);

        var financial = Assert.Single(summary.FinancialTotals!);
        Assert.Equal("CRC", financial.CurrencyCode);
        Assert.Equal(100_000m, financial.CashTotal);
        Assert.Equal(0m, financial.CreditTotal);
        Assert.Equal(0m, financial.OtherTotal);
    }

    [Fact]
    public async Task GetCategoriesAsync_EnsuresAccountingDefaultsAndDestinations()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        var service = new ElectronicDocumentService(dbContext);

        var categories = await service.GetCategoriesAsync(true, CancellationToken.None);

        Assert.Contains(categories, x =>
            x.SystemKey == "expense.fuel" &&
            x.Name == "Combustible" &&
            x.AccountingNature == AccountingNature.OperatingExpense &&
            x.DefaultOperationalDestination == OperationalDestination.InternalExpense);

        Assert.Contains(categories, x =>
            x.SystemKey == "inventory.merchandise" &&
            x.AccountingNature == AccountingNature.Inventory &&
            x.DefaultOperationalDestination is null);

        Assert.Contains(categories, x =>
            x.SystemKey == "direct.installation" &&
            x.AccountingNature == AccountingNature.DirectCost &&
            x.DefaultOperationalDestination == OperationalDestination.DirectCustomer);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByProcessingStatus()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);

        dbContext.ElectronicDocuments.Add(
            CreateDocument(
                id: 2,
                clave: "50602102600310115777600500001010000051684120639724",
                status: ElectronicDocumentProcessingStatus.Processed));
        await dbContext.SaveChangesAsync();

        var service = new ElectronicDocumentService(dbContext);
        var result = await service.GetPageAsync(
            new ElectronicDocumentListRequest
            {
                ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, result.Items[0].ProcessingStatus);
    }

    private static RevestikDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"ElectronicDocumentService-{Guid.NewGuid()}")
            .Options);

    private static async Task<ElectronicDocumentCategory> SeedAsync(RevestikDbContext dbContext)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "electronic-documents@example.com",
            NormalizedUserName = "ELECTRONIC-DOCUMENTS@EXAMPLE.COM",
            Email = "electronic-documents@example.com",
            NormalizedEmail = "ELECTRONIC-DOCUMENTS@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "Electronic Documents User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        var category = new ElectronicDocumentCategory
        {
            Name = "Inventario test",
            AccountingNature = AccountingNature.Inventory,
            IsDefaultDestinationInitialized = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ElectronicDocumentCategories.Add(category);
        dbContext.ElectronicDocuments.Add(
            CreateDocument(
                id: 1,
                clave: "50602102600310115777600500001010000051684120639723",
                status: ElectronicDocumentProcessingStatus.Pending));

        await dbContext.SaveChangesAsync();
        return category;
    }

    private static ElectronicDocumentLine CreateLine(
        int id,
        int lineNumber,
        string cabys,
        decimal totalLine,
        int? categoryId = null,
        OperationalDestination? destination = null) =>
        new()
        {
            Id = id,
            ElectronicDocumentId = 1,
            LineNumber = lineNumber,
            CabysCode = cabys,
            CommercialCodeType = string.Empty,
            CommercialCode = string.Empty,
            Quantity = 1m,
            UnitOfMeasure = "U",
            CommercialUnitOfMeasure = string.Empty,
            Description = "Línea test",
            UnitPrice = totalLine,
            GrossAmount = totalLine,
            Subtotal = totalLine,
            TaxableBase = totalLine,
            NetTax = 0m,
            TotalLine = totalLine,
            ClassificationCategoryId = categoryId,
            OperationalDestination = destination
        };

    private static ElectronicDocument CreateDocument(
        int id,
        string clave,
        ElectronicDocumentProcessingStatus status) =>
        new()
        {
            Id = id,
            Clave = clave,
            DocumentType = ElectronicDocumentType.Invoice,
            NumeroConsecutivo = "00100001010000051684",
            FechaEmision = DateTimeOffset.Now,
            IssuerEconomicActivityCode = string.Empty,
            ReceiverEconomicActivityCode = string.Empty,
            IssuerName = "EXPOCERAMICA",
            IssuerCommercialName = string.Empty,
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101157776",
            IssuerPhoneNumber = string.Empty,
            IssuerEmail = string.Empty,
            IssuerAddress = string.Empty,
            ReceiverName = "3-102-959852 SRL",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3102959852",
            SaleConditionCode = "01",
            CurrencyCode = "CRC",
            ExchangeRate = 1m,
            TotalSale = 28635.01134m,
            TotalNetSale = 25340.718m,
            TotalTax = 3294.29334m,
            TotalDocument = 28635.01134m,
            ProcessingStatus = status,
            OriginalXml = [1, 2, 3],
            ImportedByUserId = UserId,
            ImportedAtUtc = DateTime.UtcNow
        };
}
