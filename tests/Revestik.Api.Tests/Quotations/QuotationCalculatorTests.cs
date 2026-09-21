using Revestik.Api.Services.Quotations;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationCalculatorTests
{
    [Fact]
    public void CalculateLine_WithTaxIncludedPrice_SeparatesBaseAndTax()
    {
        var line = CreateLine(
            quantity: 1m,
            unitPrice: 15000m,
            taxRate: 13m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(13274.34m, result.BaseAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(1725.66m, result.TaxAmount);
        Assert.Equal(15000m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithZeroTax_PreservesPublicPrice()
    {
        var line = CreateLine(
            quantity: 1m,
            unitPrice: 15000m,
            taxRate: 0m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(15000m, result.BaseAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Equal(15000m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithMultipleQuantity_CalculatesFullLine()
    {
        var line = CreateLine(
            quantity: 20m,
            unitPrice: 15000m,
            taxRate: 13m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(265486.73m, result.BaseAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(34513.27m, result.TaxAmount);
        Assert.Equal(300000m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithPercentageDiscount_AppliesDiscountBeforeTaxSeparation()
    {
        var line = CreateLine(
            quantity: 1m,
            unitPrice: 15000m,
            taxRate: 13m,
            discountType: DiscountType.Percentage,
            discountValue: 10m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(11946.90m, result.BaseAmount);
        Assert.Equal(1500m, result.DiscountAmount);
        Assert.Equal(1553.10m, result.TaxAmount);
        Assert.Equal(13500m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithFixedDiscount_AppliesFixedAmount()
    {
        var line = CreateLine(
            quantity: 2m,
            unitPrice: 15000m,
            taxRate: 13m,
            discountType: DiscountType.FixedAmount,
            discountValue: 5000m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(22123.89m, result.BaseAmount);
        Assert.Equal(5000m, result.DiscountAmount);
        Assert.Equal(2876.11m, result.TaxAmount);
        Assert.Equal(25000m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithOneHundredPercentDiscount_ReturnsZeroTotal()
    {
        var line = CreateLine(
            quantity: 1m,
            unitPrice: 15000m,
            taxRate: 13m,
            discountType: DiscountType.Percentage,
            discountValue: 100m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(0m, result.BaseAmount);
        Assert.Equal(15000m, result.DiscountAmount);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithDecimalQuantity_CalculatesCorrectly()
    {
        var line = CreateLine(
            quantity: 1.5m,
            unitPrice: 15000m,
            taxRate: 13m);

        var result = QuotationCalculator.CalculateLine(line);

        Assert.Equal(19911.50m, result.BaseAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(2588.50m, result.TaxAmount);
        Assert.Equal(22500m, result.TotalAmount);
    }

    [Fact]
    public void CalculateLine_WithUnsupportedTaxRate_Throws()
    {
        var line = CreateLine(
            quantity: 1m,
            unitPrice: 15000m,
            taxRate: 5m);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => QuotationCalculator.CalculateLine(line));
    }

    [Fact]
    public void CalculateLine_WithNullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => QuotationCalculator.CalculateLine(null!));
    }

    private static QuotationLineRequest CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        DiscountType? discountType = null,
        decimal discountValue = 0m)
    {
        return new QuotationLineRequest
        {
            CabysCode = "1234567890123",
            Description = "Porcelanato 60x120",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountType = discountType,
            DiscountValue = discountValue,
            TaxRate = taxRate
        };
    }
}