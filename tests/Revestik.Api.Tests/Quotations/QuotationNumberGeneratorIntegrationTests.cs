using Revestik.Api.Services.Quotations;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationNumberGeneratorIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task GenerateAsync_ReturnsExpectedQuotationNumberFormat()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var generator =
            new SqlQuotationNumberGenerator(dbContext);

        var quotationNumber =
            await generator.GenerateAsync(
                CancellationToken.None);

        Assert.StartsWith(
            "COT-",
            quotationNumber);

        Assert.Equal(
            10,
            quotationNumber.Length);

        Assert.True(
            long.TryParse(
                quotationNumber["COT-".Length..],
                out _));
    }

    [Fact]
    public async Task GenerateAsync_WhenCalledSequentially_ReturnsIncreasingNumbers()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var generator =
            new SqlQuotationNumberGenerator(dbContext);

        var first =
            await generator.GenerateAsync(
                CancellationToken.None);

        var second =
            await generator.GenerateAsync(
                CancellationToken.None);

        var third =
            await generator.GenerateAsync(
                CancellationToken.None);

        var firstValue = ParseSequenceValue(first);
        var secondValue = ParseSequenceValue(second);
        var thirdValue = ParseSequenceValue(third);

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
                    new SqlQuotationNumberGenerator(
                        dbContext);

                return await generator.GenerateAsync(
                    CancellationToken.None);
            })
            .ToArray();

        var quotationNumbers =
            await Task.WhenAll(tasks);

        Assert.Equal(
            requestCount,
            quotationNumbers.Length);

        Assert.Equal(
            requestCount,
            quotationNumbers.Distinct().Count());

        Assert.All(
            quotationNumbers,
            quotationNumber =>
            {
                Assert.StartsWith(
                    "COT-",
                    quotationNumber);
            });
    }

    private static long ParseSequenceValue(
        string quotationNumber)
    {
        const string prefix = "COT-";

        Assert.StartsWith(
            prefix,
            quotationNumber);

        var numericPart =
            quotationNumber[prefix.Length..];

        Assert.True(
            long.TryParse(
                numericPart,
                out var value));

        return value;
    }
}