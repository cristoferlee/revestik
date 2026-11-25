using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Sales;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Sales;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Sales;

// REFACTOR-04B: SQL Server regression for paginated reads and streamed summaries.
public sealed class SaleReadOptimizationSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task SaleReads_MatchLegacyResults_AndLimitNormalPagesInSql()
    {
        await SeedAsync();

        await using var referenceDb = fixture.CreateDbContext();
        var legacy = await GetLegacyItemsAsync(referenceDb);
        Assert.Equal(45, legacy.Count);

        var pageCounter = new SqlCommandCounter();
        await using (var db = CreateMeasuredContext(pageCounter))
        {
            var service = CreateService(db);
            var request = new SaleListRequest { Page = 3, PageSize = 7 };
            var result = await service.GetPageAsync(request, CancellationToken.None);

            Assert.Equal(45, result.TotalCount);
            Assert.Equal(7, result.Items.Count);
            AssertSameItems(legacy.Skip(14).Take(7), result.Items);
            Assert.Equal(2, pageCounter.Count); // COUNT + SQL-paginated page.
            Assert.Contains(pageCounter.SqlTexts,
                sql => sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
        }

        // Preserve all original filters, including dates, snapshot names and currency.
        foreach (var request in new[]
        {
            new SaleListRequest { Page = 1, PageSize = 6, Status = SaleStatus.Issued },
            new SaleListRequest { Page = 2, PageSize = 4, Currency = Currency.USD },
            new SaleListRequest { Page = 1, PageSize = 8, Search = "VEN-900" },
            new SaleListRequest
            {
                Page = 1,
                PageSize = 5,
                DateFromUtc = new DateTime(2026, 11, 15, 11, 0, 0, DateTimeKind.Utc)
            }
        })
        {
            await using var db = fixture.CreateDbContext();
            var result = await CreateService(db).GetPageAsync(request, CancellationToken.None);
            var filtered = FilterLegacy(legacy, request).ToList();
            Assert.Equal(filtered.Count, result.TotalCount);
            AssertSameItems(filtered.Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize), result.Items);
        }

        // The computed balance filter deliberately retains full evaluation.
        foreach (var balance in new[]
        {
            SaleBalanceStatus.Pending,
            SaleBalanceStatus.PartiallyPaid,
            SaleBalanceStatus.Paid
        })
        {
            await using var db = fixture.CreateDbContext();
            var request = new SaleListRequest
            {
                BalanceStatus = balance,
                Page = 2,
                PageSize = 3
            };
            var result = await CreateService(db).GetPageAsync(request, CancellationToken.None);
            var expected = legacy.Where(item => item.BalanceStatus == balance).ToList();
            Assert.Equal(expected.Count, result.TotalCount);
            AssertSameItems(expected.Skip(3).Take(3), result.Items);
        }

        var emptyCounter = new SqlCommandCounter();
        await using (var db = CreateMeasuredContext(emptyCounter))
        {
            var result = await CreateService(db).GetPageAsync(
                new SaleListRequest { Page = int.MaxValue, PageSize = 100 },
                CancellationToken.None);
            Assert.Empty(result.Items);
            Assert.Equal(45, result.TotalCount);
            Assert.Equal(1, emptyCounter.Count); // Only COUNT; no data page.
        }

        // The summary must preserve per-sale rounding, active-payment exclusion,
        // voided/draft exclusion and separate CRC/USD totals.
        var expectedCurrencies = legacy
            .Where(item => item.Status == SaleStatus.Issued)
            .GroupBy(item => item.Currency)
            .OrderBy(group => group.Key)
            .Select(group => new SaleCurrencySummaryResponse
            {
                Currency = group.Key,
                SoldTotal = Round(group.Sum(item => item.Total)),
                CollectedTotal = Round(group.Sum(item => item.PaidTotal)),
                OutstandingTotal = Round(group.Sum(item => item.OutstandingAmount)),
                SaleCount = group.Count()
            }).ToList();

        var summaryCounter = new SqlCommandCounter();
        await using (var db = CreateMeasuredContext(summaryCounter))
        {
            var summary = await CreateService(db).GetSummaryAsync(CancellationToken.None);
            Assert.Equal(expectedCurrencies.Count, summary.Currencies.Count);
            foreach (var (expected, actual) in expectedCurrencies.Zip(summary.Currencies))
            {
                Assert.Equal(expected.Currency, actual.Currency);
                Assert.Equal(expected.SoldTotal, actual.SoldTotal);
                Assert.Equal(expected.CollectedTotal, actual.CollectedTotal);
                Assert.Equal(expected.OutstandingTotal, actual.OutstandingTotal);
                Assert.Equal(expected.SaleCount, actual.SaleCount);
            }
            Assert.Equal(1, summaryCounter.Count);
        }
    }

    private async Task SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "sales-read-test@revestik.invalid",
            Email = "sales-read-test@revestik.invalid",
            EmailConfirmed = true,
            DisplayName = "Sales Read Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var customer = new Customer
        {
            IdentificationType = IdentificationType.LegalEntity,
            IdentificationNumber = "3107777777",
            Name = "Cliente Consulta SQL",
            Email = "sales-sql-test@example.invalid",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var anchor = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 45; index++)
        {
            var status = index % 17 == 0 ? SaleStatus.Draft
                : index % 23 == 0 ? SaleStatus.Voided : SaleStatus.Issued;
            var sale = new Sale
            {
                SaleNumber = status == SaleStatus.Draft ? null : $"VEN-{900000 + index:000000}",
                CustomerId = customer.Id,
                CustomerNameSnapshot = status == SaleStatus.Draft ? "" : "Cliente Histórico",
                CustomerIdentificationNumberSnapshot = "3107777777",
                CustomerEmailSnapshot = "historico@example.invalid",
                CustomerPhoneNumberSnapshot = "88888888",
                CreatedByUserId = user.Id,
                IssuedByUserId = status == SaleStatus.Draft ? null : user.Id,
                Currency = index % 2 == 0 ? Currency.CRC : Currency.USD,
                Status = status,
                IssuedAtUtc = status == SaleStatus.Draft ? null : anchor.AddMinutes(-index),
                CreatedAtUtc = anchor.AddMinutes(-index),
                VoidedAtUtc = status == SaleStatus.Voided ? anchor : null,
                VoidReason = status == SaleStatus.Voided ? "Historical test void" : "",
                GeneralDiscountType = index % 5 == 0 ? DiscountType.Percentage : null,
                GeneralDiscountValue = index % 5 == 0 ? 7.5m : 0m,
                Lines =
                [
                    new SaleLine
                    {
                        Description = "Cerámica", Unit = "m2", Quantity = 2m,
                        UnitPrice = 125m + index, TaxRate = 13m,
                        DiscountType = index % 4 == 0 ? DiscountType.Percentage : null,
                        DiscountValue = index % 4 == 0 ? 5m : 0m
                    },
                    new SaleLine
                    {
                        Description = "Accesorio", Unit = "unid", Quantity = 1m,
                        UnitPrice = 49.99m, TaxRate = 0m
                    }
                ],
                Charges =
                [
                    new SaleCharge
                    {
                        Type = SaleChargeType.Transport,
                        Description = "Transporte", Amount = 10m
                    }
                ]
            };

            if (status == SaleStatus.Issued && index % 3 != 0)
            {
                var total = SaleCalculator.Calculate(
                    sale.Lines.Select(MapLine).ToList(),
                    sale.Charges.Select(charge => new SaleChargeRequest
                    {
                        Amount = charge.Amount
                    }).ToList(),
                    sale.GeneralDiscountType,
                    sale.GeneralDiscountValue).Total;

                sale.Payments.Add(new SalePayment
                {
                    Amount = index % 3 == 2 ? total : 25m,
                    PaymentMethod = PaymentMethod.Cash,
                    Status = SalePaymentStatus.Active,
                    PaidAtUtc = anchor, CreatedAtUtc = anchor,
                    CreatedByUserId = user.Id
                });
                sale.Payments.Add(new SalePayment
                {
                    Amount = 99m,
                    PaymentMethod = PaymentMethod.Sinpe,
                    Status = SalePaymentStatus.Voided,
                    PaidAtUtc = anchor, CreatedAtUtc = anchor,
                    VoidedAtUtc = anchor, VoidedByUserId = user.Id,
                    VoidReason = "Void test", CreatedByUserId = user.Id
                });
            }
            db.Sales.Add(sale);
        }
        await db.SaveChangesAsync();
    }

    private static async Task<List<SaleListItemResponse>> GetLegacyItemsAsync(
        RevestikDbContext db)
    {
        var sales = await db.Sales.AsNoTracking().AsSingleQuery()
            .Include(sale => sale.Customer)
            .Include(sale => sale.Lines)
            .Include(sale => sale.Charges)
            .Include(sale => sale.Payments)
            .OrderByDescending(sale => sale.IssuedAtUtc ?? sale.CreatedAtUtc)
            .ThenByDescending(sale => sale.Id)
            .ToListAsync();

        return sales.Select(sale =>
        {
            var total = SaleCalculator.Calculate(
                sale.Lines.OrderBy(line => line.Id).Select(MapLine).ToList(),
                sale.Charges.Select(charge => new SaleChargeRequest
                {
                    Type = charge.Type,
                    Description = charge.Description,
                    Amount = charge.Amount
                }).ToList(),
                sale.GeneralDiscountType,
                sale.GeneralDiscountValue).Total;
            var paid = Round(sale.Payments
                .Where(payment => payment.Status == SalePaymentStatus.Active)
                .Sum(payment => payment.Amount));
            return new SaleListItemResponse
            {
                Id = sale.Id,
                SaleNumber = sale.SaleNumber ?? "",
                SourceQuotationNumber = "",
                IssuedAtUtc = sale.IssuedAtUtc,
                CustomerName = sale.Status != SaleStatus.Draft &&
                    sale.CustomerNameSnapshot != ""
                    ? sale.CustomerNameSnapshot : sale.Customer.Name,
                Currency = sale.Currency,
                Status = sale.Status,
                Total = total,
                PaidTotal = paid,
                OutstandingAmount = Math.Max(0m, Round(total - paid)),
                BalanceStatus = paid <= 0m ? SaleBalanceStatus.Pending :
                    paid >= total ? SaleBalanceStatus.Paid : SaleBalanceStatus.PartiallyPaid
            };
        }).ToList();
    }

    private static IEnumerable<SaleListItemResponse> FilterLegacy(
        IEnumerable<SaleListItemResponse> items, SaleListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
            items = items.Where(item =>
                item.SaleNumber.Contains(request.Search.Trim(), StringComparison.Ordinal) ||
                // The seed deliberately has the same current customer name.
                "Cliente Consulta SQL".Contains(request.Search.Trim(), StringComparison.Ordinal));
        if (request.Status.HasValue)
            items = items.Where(item => item.Status == request.Status);
        if (request.Currency.HasValue)
            items = items.Where(item => item.Currency == request.Currency);
        if (request.DateFromUtc.HasValue)
            items = items.Where(item => item.IssuedAtUtc >= request.DateFromUtc.Value);
        if (request.DateToUtc.HasValue)
            items = items.Where(item => item.IssuedAtUtc <= request.DateToUtc.Value);
        return items;
    }

    private static SaleLineRequest MapLine(SaleLine line) => new()
    {
        ProductId = line.ProductId,
        CabysCode = line.CabysCode ?? "",
        Description = line.Description,
        Unit = line.Unit,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        DiscountType = line.DiscountType,
        DiscountValue = line.DiscountValue,
        TaxRate = line.TaxRate
    };

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void AssertSameItems(
        IEnumerable<SaleListItemResponse> expected,
        IReadOnlyList<SaleListItemResponse> actual)
    {
        var list = expected.ToList();
        Assert.Equal(list.Count, actual.Count);
        for (var index = 0; index < list.Count; index++)
        {
            var left = list[index];
            var right = actual[index];
            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.SaleNumber, right.SaleNumber);
            Assert.Equal(left.SourceQuotationNumber, right.SourceQuotationNumber);
            Assert.Equal(left.IssuedAtUtc, right.IssuedAtUtc);
            Assert.Equal(left.CustomerName, right.CustomerName);
            Assert.Equal(left.Currency, right.Currency);
            Assert.Equal(left.Status, right.Status);
            Assert.Equal(left.Total, right.Total);
            Assert.Equal(left.PaidTotal, right.PaidTotal);
            Assert.Equal(left.OutstandingAmount, right.OutstandingAmount);
            Assert.Equal(left.BalanceStatus, right.BalanceStatus);
        }
    }

    private RevestikDbContext CreateMeasuredContext(SqlCommandCounter counter)
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(counter)
            .Options;
        return new RevestikDbContext(options);
    }

    private static SaleService CreateService(RevestikDbContext db) =>
        new(db, new FakeSaleNumberGenerator());

    private sealed class FakeSaleNumberGenerator : ISaleNumberGenerator
    {
        public Task<string> GenerateAsync(CancellationToken cancellationToken) =>
            Task.FromResult("VEN-999999");
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private readonly List<string> sqlTexts = [];
        public int Count => sqlTexts.Count;
        public IReadOnlyList<string> SqlTexts => sqlTexts;
        private void Record(DbCommand command) => sqlTexts.Add(command.CommandText);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return new ValueTask<DbDataReader>(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData,
            object? result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return new ValueTask<object?>(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return new ValueTask<int>(result);
        }
    }
}
