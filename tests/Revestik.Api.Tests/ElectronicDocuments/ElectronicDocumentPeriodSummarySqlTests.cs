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

// REFACTOR-04D.2: one SQL fixture and one fact, checking filtered and unfiltered summaries.
// No extra container or catalog import, and no production database access.
public sealed class ElectronicDocumentPeriodSummarySqlTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task PeriodAndUnfilteredSummaries_PreserveXmlDatesTotalsAndSqlCommandCounts()
    {
        await SeedAsync();

        var periodCounter = new CommandCounter();
        await using (var db = MeasuredContext(periodCounter))
        {
            var result = await new ElectronicDocumentPeriodSummaryService(db)
                .GetSummaryAsync(new ElectronicDocumentSummaryRequest
                {
                    DateFrom = new DateOnly(2026, 10, 1),
                    DateTo = new DateOnly(2026, 10, 1)
                }, CancellationToken.None);

            // One narrow ID/date query, one grouped count, one classified read.
            Assert.Equal(3, periodCounter.Count);
            Assert.Equal(1, result.PendingCount);
            Assert.Equal(5, result.ProcessedCount);
            Assert.Equal(1, result.NoActionRequiredCount);
            Assert.Equal(1, result.WithHaciendaResponseCount);

            var accounting = result.AccountingTotals!.ToDictionary(x => x.CurrencyCode);
            Assert.Equal(2, accounting.Count);
            Assert.Equal(100m, accounting["CRC"].PurchasesTotal);
            Assert.Equal(40m, accounting["CRC"].ExpensesTotal);
            Assert.Equal(90m, accounting["CRC"].DirectCostsTotal);
            Assert.Equal(70m, accounting["CRC"].UnallocatedTotal);
            Assert.Equal(9.5m, accounting["USD"].ExpensesTotal);

            var financial = result.FinancialTotals!.ToDictionary(x => x.CurrencyCode);
            Assert.Equal(100m, financial["CRC"].CashTotal);
            Assert.Equal(200m, financial["CRC"].CreditTotal);
            Assert.Equal(9.5m, financial["USD"].OtherTotal);
        }

        var unfilteredCounter = new CommandCounter();
        await using (var db = MeasuredContext(unfilteredCounter))
        {
            var result = await new ElectronicDocumentPeriodSummaryService(db)
                .GetSummaryAsync(new ElectronicDocumentSummaryRequest(), CancellationToken.None);

            // Without a date filter there is no candidate-date SELECT.
            Assert.Equal(2, unfilteredCounter.Count);
            Assert.Equal(1, result.PendingCount);
            Assert.Equal(7, result.ProcessedCount);
            Assert.Equal(1, result.NoActionRequiredCount);
            Assert.Equal(1, result.WithHaciendaResponseCount);

            var accounting = result.AccountingTotals!.ToDictionary(x => x.CurrencyCode);
            Assert.Equal(1_200m, accounting["CRC"].PurchasesTotal);
            var financial = result.FinancialTotals!.ToDictionary(x => x.CurrencyCode);
            Assert.Equal(1_200m, financial["CRC"].CashTotal);
            Assert.Equal(200m, financial["CRC"].CreditTotal);
        }

        var emptyCounter = new CommandCounter();
        await using (var db = MeasuredContext(emptyCounter))
        {
            var result = await new ElectronicDocumentPeriodSummaryService(db)
                .GetSummaryAsync(new ElectronicDocumentSummaryRequest
                {
                    DateFrom = new DateOnly(2026, 8, 1),
                    DateTo = new DateOnly(2026, 8, 1)
                }, CancellationToken.None);
            Assert.Equal(3, emptyCounter.Count);
            Assert.Equal(0, result.PendingCount);
            Assert.Equal(0, result.ProcessedCount);
            Assert.Equal(0, result.NoActionRequiredCount);
            Assert.Equal(0, result.WithHaciendaResponseCount);
            Assert.Empty(result.AccountingTotals!);
            Assert.Empty(result.FinancialTotals!);
        }
    }

    private RevestikDbContext MeasuredContext(CommandCounter counter)
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
            Id = Guid.NewGuid().ToString("N"),
            UserName = "04d2-period@example.test",
            NormalizedUserName = "04D2-PERIOD@EXAMPLE.TEST",
            Email = "04d2-period@example.test",
            NormalizedEmail = "04D2-PERIOD@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "04D2 Period SQL Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var inventory = Category("04D2 - Inventory", AccountingNature.Inventory);
        var expense = Category("04D2 - Expense", AccountingNature.OperatingExpense);
        var direct = Category("04D2 - Direct", AccountingNature.DirectCost);
        db.Users.Add(user);
        db.ElectronicDocumentCategories.AddRange(inventory, expense, direct);
        await db.SaveChangesAsync();

        var day = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-6));
        var documents = new[]
        {
            // Both extreme offsets have October 1 as the date stored in XML.
            Document(1, user.Id, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(14)),
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "01", 100m),
            Document(2, user.Id, new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.FromHours(-14)),
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "02", 200m),
            Document(3, user.Id, day,
                ElectronicDocumentType.ElectronicTicket, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, expense, "USD", "99", 9.5m),
            Document(4, user.Id, day,
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Pending, null, "CRC", "01", 15m),
            Document(5, user.Id, day,
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.NoActionRequired, null, "CRC", "01", 25m),
            Document(6, user.Id, day,
                ElectronicDocumentType.CreditNote, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "01", 30m),
            Document(7, user.Id, day,
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Issued,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "01", 40m),
            // Nearby UTC instants must not make neighboring XML dates eligible.
            Document(8, user.Id, new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-14)),
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "01", 500m),
            Document(9, user.Id, new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(14)),
                ElectronicDocumentType.Invoice, ElectronicDocumentDirection.Received,
                ElectronicDocumentProcessingStatus.Processed, inventory, "CRC", "01", 600m)
        };

        documents[1].Lines.Add(Line(1, 40m, expense));
        documents[1].Lines.Add(Line(2, 90m, direct));
        documents[0].HaciendaResponse = new HaciendaResponse
        {
            Clave = documents[0].Clave,
            MessageCode = "1",
            MessageStatus = "Accepted",
            OriginalXml = [1],
            ReceivedAtUtc = DateTime.UtcNow
        };

        db.ElectronicDocuments.AddRange(documents);
        await db.SaveChangesAsync();
    }

    private static ElectronicDocumentCategory Category(string name, AccountingNature nature) => new()
    {
        Name = name,
        AccountingNature = nature,
        IsActive = true,
        IsDefaultDestinationInitialized = true,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static ElectronicDocument Document(
        int index, string userId, DateTimeOffset date,
        ElectronicDocumentType type, ElectronicDocumentDirection direction,
        ElectronicDocumentProcessingStatus status, ElectronicDocumentCategory? category,
        string currency, string condition, decimal total) => new()
    {
        Clave = index.ToString().PadLeft(50, '0'),
        NumeroConsecutivo = index.ToString().PadLeft(20, '0'),
        DocumentType = type,
        Direction = direction,
        FechaEmision = date,
        IssuerName = "04D2 Test Issuer",
        IssuerIdentificationType = "02",
        IssuerIdentification = "3101000000",
        ReceiverName = "04D2 Test Receiver",
        ReceiverIdentificationType = "02",
        ReceiverIdentification = "3102959852",
        CurrencyCode = currency,
        ExchangeRate = 1m,
        SaleConditionCode = condition,
        TotalDocument = total,
        Category = category,
        ProcessingStatus = status,
        OriginalXml = [1, 2, 3],
        ImportedByUserId = userId,
        ImportedAtUtc = DateTime.UtcNow
    };

    private static ElectronicDocumentLine Line(
        int number, decimal total, ElectronicDocumentCategory category) => new()
    {
        LineNumber = number,
        CabysCode = "1234567890123",
        Quantity = 1m,
        UnitOfMeasure = "U",
        Description = "04D2 period test line",
        UnitPrice = total,
        TotalLine = total,
        ClassificationCategory = category,
        OperationalDestination = category.AccountingNature == AccountingNature.DirectCost
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
