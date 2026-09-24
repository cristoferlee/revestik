using Revestik.Api.Services.Sales;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleNumberGeneratorIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task GenerateAsync_ReturnsExpectedSaleNumberFormat()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var generator =
            new SqlSaleNumberGenerator(dbContext);

        var saleNumber =
            await generator.GenerateAsync(
                CancellationToken.None);

        Assert.StartsWith(
            "VEN-",
            saleNumber);

        Assert.Equal(
            10,
            saleNumber.Length);

        Assert.True(
            long.TryParse(
                saleNumber["VEN-".Length..],
                out _));
    }

    [Fact]
    public async Task GenerateAsync_WhenCalledSequentially_ReturnsIncreasingNumbers()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var generator =
            new SqlSaleNumberGenerator(dbContext);

        var first =
            await generator.GenerateAsync(
                CancellationToken.None);

        var second =
            await generator.GenerateAsync(
                CancellationToken.None);

        var third =
            await generator.GenerateAsync(
                CancellationToken.None);

        var firstValue =
            ParseSequenceValue(first);

        var secondValue =
            ParseSequenceValue(second);

        var thirdValue =
            ParseSequenceValue(third);

        Assert.Equal(
            firstValue + 1,
            secondValue);

        Assert.Equal(
            secondValue + 1,
            thirdValue);
    }

    [Fact]
    public async Task GenerateAsync_WhenCalledConcurrently_ReturnsUniqueNumbers()
    {
        const int requestCount = 20;

        var tasks = Enumerable
            .Range(0, requestCount)
            .Select(async _ =>
            {
                await using var dbContext =
                    sqlServerFixture.CreateDbContext();

                var generator =
                    new SqlSaleNumberGenerator(
                        dbContext);

                return await generator.GenerateAsync(
                    CancellationToken.None);
            })
            .ToArray();

        var saleNumbers =
            await Task.WhenAll(tasks);

        Assert.Equal(
            requestCount,
            saleNumbers.Length);

        Assert.Equal(
            requestCount,
            saleNumbers
                .Distinct()
                .Count());

        Assert.All(
            saleNumbers,
            saleNumber =>
            {
                Assert.StartsWith(
                    "VEN-",
                    saleNumber);
            });
    }

    private static long ParseSequenceValue(
        string saleNumber)
    {
        const string prefix = "VEN-";

        Assert.StartsWith(
            prefix,
            saleNumber);

        var numericPart =
            saleNumber[prefix.Length..];

        Assert.True(
            long.TryParse(
                numericPart,
                out var value));

        return value;
    }
}