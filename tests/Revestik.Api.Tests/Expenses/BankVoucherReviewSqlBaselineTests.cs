using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.BankVouchers;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

// REFACTOR-02A.2: SQL baseline only. No production configuration or service changes.
public sealed class BankVoucherReviewSqlBaselineTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly object MetricsLock = new();

    [Fact]
    public async Task AcceptedVouchers_ReportsSqlBaselineAtThreeVolumes()
    {
        foreach (var (count, month) in new[]
        {
            (Count: 10, Month: 1),
            (Count: 50, Month: 2),
            (Count: 100, Month: 3)
        })
        {
            var day = new DateOnly(2026, month, 15);

            await using (var seed = fixture.CreateDbContext())
            {
                seed.BankVouchers.AddRange(
                    Enumerable.Range(0, count)
                        .Select(index => CreateVoucher(
                            day, index, BankVoucherStatus.Accepted)));

                await seed.SaveChangesAsync();
            }

            var counter = new SqlCommandCounter();

            await using var db = CreateMeasuredContext(counter);

            var watch = Stopwatch.StartNew();

            var result = await new BankVoucherReviewService(db).GetAsync(
                new BankVoucherListRequest
                {
                    DateFrom = day,
                    DateTo = day,
                    Status = BankVoucherStatus.Accepted,
                    Page = 1,
                    PageSize = 10
                },
                CancellationToken.None);

            watch.Stop();

            Assert.Equal(count, result.TotalCount);
            Assert.Equal(Math.Min(10, count), result.Items.Count);
            Assert.All(result.Items,
                item => Assert.Equal(BankVoucherStatus.Accepted, item.Status));
            Assert.True(counter.Count > 0);

            Record(
                "AcceptedNoCandidates",
                count,
                counter.Count,
                watch.Elapsed.TotalMilliseconds,
                result.Items.Count,
                result.TotalCount);
        }
    }

    [Fact]
    public async Task LateCandidate_ReturnsAcceptedVoucherToReviewOnSqlServer()
    {
        var day = new DateOnly(2026, 4, 15);
        string voucherMessageId;

        await using (var seed = fixture.CreateDbContext())
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"voucher-{Guid.NewGuid():N}@example.test",
                NormalizedUserName = $"VOUCHER-{Guid.NewGuid():N}@EXAMPLE.TEST"
            };

            seed.Users.Add(user);

            var voucher = CreateVoucher(
                day, 0, BankVoucherStatus.Accepted);

            voucherMessageId = voucher.GmailMessageId;

            var issuedAt = new DateTimeOffset(
                2026, 4, 16, 10, 0, 0, TimeSpan.FromHours(-6));

            seed.ElectronicDocuments.Add(new ElectronicDocument
            {
                Clave = Guid.NewGuid().ToString("N").PadRight(50, '0'),
                NumeroConsecutivo = "00100001010000000001",
                DocumentType = ElectronicDocumentType.Invoice,
                Direction = ElectronicDocumentDirection.Received,
                FechaEmision = issuedAt,
                IssuerName = voucher.MerchantName,
                IssuerCommercialName = voucher.MerchantName,
                IssuerEconomicActivityCode = "000000",
                ReceiverEconomicActivityCode = "000000",
                IssuerIdentificationType = "02",
                IssuerIdentification = "3101000000",
                ReceiverIdentificationType = "02",
                ReceiverIdentification = "3101000000",
                ReceiverName = "Revestik Test",
                CurrencyCode = voucher.Currency,
                ExchangeRate = 1m,
                TotalDocument = voucher.Amount,
                SaleConditionCode = "01",
                ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
                OriginalXml = [1],
                ImportedByUserId = user.Id,
                ImportedAtUtc = DateTime.UtcNow
            });

            seed.BankVouchers.Add(voucher);
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();

        await using (var db = CreateMeasuredContext(counter))
        {
            var watch = Stopwatch.StartNew();

            var result = await new BankVoucherReviewService(db).GetAsync(
                new BankVoucherListRequest
                {
                    DateFrom = day,
                    DateTo = day,
                    Status = BankVoucherStatus.NeedsReview,
                    Page = 1,
                    PageSize = 10
                },
                CancellationToken.None);

            watch.Stop();

            var item = Assert.Single(result.Items);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(BankVoucherStatus.NeedsReview, item.Status);
            Assert.Single(item.MatchCandidates);
            Assert.True(counter.Count > 0);

            Record(
                "LateCandidate",
                1,
                counter.Count,
                watch.Elapsed.TotalMilliseconds,
                result.Items.Count,
                result.TotalCount);
        }

        await using var verify = fixture.CreateDbContext();

        var storedStatus = await verify.BankVouchers
            .AsNoTracking()
            .Where(x => x.GmailMessageId == voucherMessageId)
            .Select(x => x.Status)
            .SingleAsync();

        Assert.Equal(BankVoucherStatus.NeedsReview, storedStatus);
    }

    [Fact]
    public async Task KnownSupplierWithoutCandidate_DoesNotQueryHistoricalIssuers()
    {
        var day = new DateOnly(2026, 5, 15);
        const string merchant = "KNOWN SUPPLIER 02A4";

        await using (var seed = fixture.CreateDbContext())
        {
            seed.Suppliers.Add(new Supplier
            {
                Name = merchant,
                CommercialName = merchant,
                ContactName = string.Empty,
                PhoneNumber = string.Empty,
                Email = string.Empty,
                Address = string.Empty,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            var voucher = CreateVoucher(day, 0, BankVoucherStatus.NeedsReview);
            voucher.MerchantName = merchant;
            seed.BankVouchers.Add(voucher);
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);

        var result = await new BankVoucherReviewService(db).GetAsync(
            new BankVoucherListRequest
            {
                DateFrom = day,
                DateTo = day,
                Status = BankVoucherStatus.NeedsReview
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsKnownMerchant);
        Assert.Empty(item.MatchCandidates);
        Assert.Equal(0, counter.HistoricalIssuerQueryCount);
    }

    [Fact]
    public async Task UnknownSupplierWithHistoricalIssuer_StillQueriesIssuerHistory()
    {
        var day = new DateOnly(2026, 6, 15);
        const string merchant = "HISTORICAL MERCHANT 02A4";

        await using (var seed = fixture.CreateDbContext())
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"history-{Guid.NewGuid():N}@example.test",
                NormalizedUserName = $"HISTORY-{Guid.NewGuid():N}@EXAMPLE.TEST"
            };

            seed.Users.Add(user);

            seed.ElectronicDocuments.Add(new ElectronicDocument
            {
                Clave = Guid.NewGuid().ToString("N").PadRight(50, '0'),
                NumeroConsecutivo = "00100001010000000001",
                DocumentType = ElectronicDocumentType.Invoice,
                Direction = ElectronicDocumentDirection.Received,
                FechaEmision = new DateTimeOffset(
                    2026, 5, 1, 10, 0, 0, TimeSpan.FromHours(-6)),
                IssuerName = merchant,
                IssuerCommercialName = merchant,
                IssuerEconomicActivityCode = "000000",
                ReceiverEconomicActivityCode = "000000",
                IssuerIdentificationType = "02",
                IssuerIdentification = "3101000000",
                ReceiverIdentificationType = "02",
                ReceiverIdentification = "3101000000",
                ReceiverName = "Revestik Test",
                CurrencyCode = "CRC",
                ExchangeRate = 1m,
                TotalDocument = 7243.47m,
                SaleConditionCode = "01",
                ProcessingStatus = ElectronicDocumentProcessingStatus.Processed,
                OriginalXml = [1],
                ImportedByUserId = user.Id,
                ImportedAtUtc = DateTime.UtcNow
            });

            var voucher = CreateVoucher(day, 0, BankVoucherStatus.NeedsReview);
            voucher.MerchantName = merchant;
            seed.BankVouchers.Add(voucher);
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);

        var result = await new BankVoucherReviewService(db).GetAsync(
            new BankVoucherListRequest
            {
                DateFrom = day,
                DateTo = day,
                Status = BankVoucherStatus.NeedsReview
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsKnownMerchant);
        Assert.Empty(item.MatchCandidates);
        Assert.True(counter.HistoricalIssuerQueryCount > 0);
    }

    private RevestikDbContext CreateMeasuredContext(SqlCommandCounter counter)
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(counter)
            .Options;

        return new RevestikDbContext(options);
    }

    private static BankVoucher CreateVoucher(
        DateOnly day, int index, BankVoucherStatus status) => new()
    {
        Bank = "Banco de prueba",
        MerchantName = "IKAMI HOME",
        Amount = 7243.47m,
        Currency = "CRC",
        TransactionDate = new DateTimeOffset(
            day.Year, day.Month, day.Day,
            11, 0, 0, TimeSpan.FromHours(-6)).AddSeconds(index),
        GmailMessageId = Guid.NewGuid().ToString("N"),
        Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static void Record(
        string scenario, int vouchers, int commands,
        double milliseconds, int returned, int total)
    {
        var elapsed = milliseconds.ToString(
            "F2", CultureInfo.InvariantCulture);

        var line = string.Join(",",
            scenario, vouchers, commands, elapsed, returned, total);

        Console.WriteLine($"BANK_VOUCHER_SQL_BASELINE: {line}");

        var path = Environment.GetEnvironmentVariable(
            "REVESTIK_BANK_VOUCHER_METRICS_PATH");

        if (string.IsNullOrWhiteSpace(path))
            return;

        lock (MetricsLock)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
                File.WriteAllText(path,
                    "Scenario,Vouchers,SqlCommands,ElapsedMs,Returned,TotalCount"
                    + Environment.NewLine);

            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private int count;
        private int historicalIssuerQueryCount;

        public int Count => Volatile.Read(ref count);
        public int HistoricalIssuerQueryCount =>
            Volatile.Read(ref historicalIssuerQueryCount);

        private void RecordCommand(DbCommand command)
        {
            Interlocked.Increment(ref count);

            var sql = command.CommandText;
            if (sql.Contains("FROM [ElectronicDocuments]", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("[IssuerName]", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("[IssuerCommercialName]", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains("[TotalDocument]", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref historicalIssuerQueryCount);
            }
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            RecordCommand(command);
            return new ValueTask<DbDataReader>(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            RecordCommand(command);
            return new ValueTask<object?>(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            RecordCommand(command);
            return new ValueTask<int>(result);
        }
    }
}
