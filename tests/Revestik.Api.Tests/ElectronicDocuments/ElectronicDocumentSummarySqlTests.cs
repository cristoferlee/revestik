using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

// REFACTOR-04C: One SQL Server fixture, one test, no extra container or migrations.
public sealed class ElectronicDocumentSummarySqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task GetSummaryAsync_UsesTwoSqlCommands_AndPreservesAllTotals()
    {
        // Empty database: the grouped count query must return zero, not null.
        var emptyCounter = new CommandCounter();
        await using (var emptyDb = CreateMeasuredContext(emptyCounter))
        {
            var empty = await new ElectronicDocumentService(emptyDb)
                .GetSummaryAsync(CancellationToken.None);
            Assert.Equal(0, empty.PendingCount);
            Assert.Equal(0, empty.ProcessedCount);
            Assert.Equal(0, empty.NoActionRequiredCount);
            Assert.Equal(0, empty.WithHaciendaResponseCount);
            Assert.Empty(empty.AccountingTotals!);
            Assert.Empty(empty.FinancialTotals!);
            Assert.Equal(2, emptyCounter.Count);
        }

        await SeedAsync();

        var counter = new CommandCounter();
        await using var db = CreateMeasuredContext(counter);
        var summary = await new ElectronicDocumentService(db)
            .GetSummaryAsync(CancellationToken.None);

        // Four original COUNT calls + two materializations are now two SQL commands.
        Assert.Equal(2, counter.Count);
        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(8, summary.ProcessedCount);
        Assert.Equal(1, summary.NoActionRequiredCount);
        Assert.Equal(1, summary.WithHaciendaResponseCount);

        var accounting = summary.AccountingTotals!
            .ToDictionary(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3, accounting.Count);
        Assert.Equal(130m, accounting["CRC"].PurchasesTotal); // 100 + 25 + lowercase crc 5
        Assert.Equal(40m, accounting["CRC"].ExpensesTotal);
        Assert.Equal(90m, accounting["CRC"].DirectCostsTotal);
        Assert.Equal(20m, accounting["CRC"].UnallocatedTotal);
        Assert.Equal(60m, accounting["USD"].ExpensesTotal);
        Assert.Equal(7.5m, accounting["N/D"].ExpensesTotal);

        var financial = summary.FinancialTotals!
            .ToDictionary(x => x.CurrencyCode, StringComparer.Ordinal);
        Assert.Equal(4, financial.Count); // CRC and crc stay distinct, as in legacy GroupBy.
        Assert.Equal(100m, financial["CRC"].CashTotal);
        Assert.Equal(175m, financial["CRC"].CreditTotal);
        Assert.Equal(0m, financial["CRC"].OtherTotal);
        Assert.Equal(5m, financial["crc"].CashTotal);
        Assert.Equal(60m, financial["USD"].OtherTotal);
        Assert.Equal(7.5m, financial["N/D"].OtherTotal);
    }

    private RevestikDbContext CreateMeasuredContext(CommandCounter counter)
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(counter)
            .Options;
        return new RevestikDbContext(options);
    }

    private async Task SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "summary04c@revestik.invalid",
            Email = "summary04c@revestik.invalid",
            EmailConfirmed = true,
            DisplayName = "Summary 04C Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var inventory = Category("Summary 04C - Inventory", AccountingNature.Inventory);
        var expense = Category("Summary 04C - Expense", AccountingNature.OperatingExpense);
        var direct = Category("Summary 04C - Direct", AccountingNature.DirectCost);
        db.Users.Add(user);
        db.ElectronicDocumentCategories.AddRange(inventory, expense, direct);
        await db.SaveChangesAsync();

        var documents = new[]
        {
            Document(1, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                inventory, "CRC", "01", 100m),
            Document(2, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                inventory, "CRC", "02", 150m),
            Document(3, user.Id, ElectronicDocumentType.ElectronicTicket,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                expense, "USD", "03", 60m),
            Document(4, user.Id, ElectronicDocumentType.CreditNote,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                inventory, "CRC", "01", 70m),
            Document(5, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Issued, ElectronicDocumentProcessingStatus.Processed,
                inventory, "CRC", "01", 80m),
            Document(6, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Pending,
                null, "CRC", "01", 30m),
            Document(7, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.NoActionRequired,
                null, "CRC", "01", 40m),
            Document(8, user.Id, ElectronicDocumentType.PurchaseInvoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                inventory, "CRC", "02", 25m),
            Document(9, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                expense, "", "99", 7.5m),
            Document(10, user.Id, ElectronicDocumentType.Invoice,
                ElectronicDocumentDirection.Received, ElectronicDocumentProcessingStatus.Processed,
                inventory, "crc", "01", 5m)
        };

        documents[1].Lines.Add(Line(1, 40m, expense));
        documents[1].Lines.Add(Line(2, 90m, direct));
        documents[7].Lines.Add(Line(1, 10m, null)); // No override: use document total (25).
        documents[3].HaciendaResponse = new HaciendaResponse
        {
            Clave = documents[3].Clave,
            MessageCode = "1",
            MessageStatus = "Accepted",
            OriginalXml = [1, 2, 3],
            ReceivedAtUtc = DateTime.UtcNow
        };

        db.ElectronicDocuments.AddRange(documents);
        await db.SaveChangesAsync();
    }

    private static ElectronicDocumentCategory Category(string name, AccountingNature nature) => new()
    {
        Name = name,
        AccountingNature = nature,
        IsDefaultDestinationInitialized = true,
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static ElectronicDocument Document(
        int index, string userId, ElectronicDocumentType type,
        ElectronicDocumentDirection direction, ElectronicDocumentProcessingStatus status,
        ElectronicDocumentCategory? category, string currency, string condition, decimal total) => new()
    {
        Clave = $"5060210260031011577760050000101000005168412063{index:D4}",
        DocumentType = type,
        Direction = direction,
        NumeroConsecutivo = $"0010000101000000{index:D4}",
        FechaEmision = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
        IssuerName = "Summary Test Issuer",
        IssuerIdentificationType = "02",
        IssuerIdentification = "3101157776",
        ReceiverName = "Revestik Test",
        ReceiverIdentificationType = "02",
        ReceiverIdentification = "3102959852",
        SaleConditionCode = condition,
        CurrencyCode = currency,
        ExchangeRate = 1m,
        TotalDocument = total,
        ProcessingStatus = status,
        Category = category,
        OriginalXml = [1, 2, 3],
        ImportedByUserId = userId,
        ImportedAtUtc = DateTime.UtcNow
    };

    private static ElectronicDocumentLine Line(
        int number, decimal total, ElectronicDocumentCategory? category) => new()
    {
        LineNumber = number,
        CabysCode = "1234567890123",
        Quantity = 1m,
        UnitOfMeasure = "U",
        Description = "Summary 04C line",
        UnitPrice = total,
        TotalLine = total,
        ClassificationCategory = category,
        OperationalDestination = category is null ? null :
            category.AccountingNature == AccountingNature.DirectCost
                ? OperationalDestination.DirectCustomer
                : OperationalDestination.InternalExpense
    };

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int count;
        public int Count => Volatile.Read(ref count);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return new ValueTask<DbDataReader>(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData,
            object? result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return new ValueTask<object?>(result);
        }
    }
}
