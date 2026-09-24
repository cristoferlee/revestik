using Revestik.Api.Services.Sales;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleCalculatorTests
{
    [Fact]
    public void Calculate_WithNoGeneralDiscount_PreservesLineMath()
    {
        var result = SaleCalculator.Calculate(
            [
                CreateLine(
                    quantity: 2m,
                    unitPrice: 15000m,
                    taxRate: 13m,
                    DiscountType.Percentage,
                    10m)
            ],
            [],
            null,
            0m);

        var line = Assert.Single(result.Lines);

        Assert.Equal(23893.81m, line.BaseAmount);
        Assert.Equal(3000m, line.LineDiscountAmount);
        Assert.Equal(0m, line.GeneralDiscountAmount);
        Assert.Equal(3106.19m, line.TaxAmount);
        Assert.Equal(27000m, line.TotalAmount);
        Assert.Equal(27000m, result.Total);
    }

    [Fact]
    public void Calculate_WithPercentageGeneralDiscount_AppliesToLines()
    {
        var result = SaleCalculator.Calculate(
            [
                CreateLine(
                    quantity: 1m,
                    unitPrice: 11300m,
                    taxRate: 13m),
                CreateLine(
                    quantity: 1m,
                    unitPrice: 10000m,
                    taxRate: 0m)
            ],
            [],
            DiscountType.Percentage,
            10m);

        Assert.Equal(2130m, result.GeneralDiscountTotal);
        Assert.Equal(19170m, result.Total);

        Assert.Equal(
            1130m,
            result.Lines[0].GeneralDiscountAmount);

        Assert.Equal(
            1000m,
            result.Lines[1].GeneralDiscountAmount);

        Assert.Equal(
            9000m,
            result.Lines[0].BaseAmount);

        Assert.Equal(
            1170m,
            result.Lines[0].TaxAmount);

        Assert.Equal(
            9000m,
            result.Lines[1].BaseAmount);

        Assert.Equal(
            0m,
            result.Lines[1].TaxAmount);
    }

    [Fact]
    public void Calculate_GeneralDiscount_DoesNotDiscountCharges()
    {
        var result = SaleCalculator.Calculate(
            [
                CreateLine(
                    quantity: 1m,
                    unitPrice: 10000m,
                    taxRate: 0m)
            ],
            [
                new SaleChargeRequest
                {
                    Type = SaleChargeType.Transport,
                    Amount = 5000m
                }
            ],
            DiscountType.Percentage,
            10m);

        Assert.Equal(1000m, result.GeneralDiscountTotal);
        Assert.Equal(5000m, result.ChargeTotal);
        Assert.Equal(14000m, result.Total);
    }

    [Fact]
    public void Calculate_FixedGeneralDiscount_CannotExceedLineTotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SaleCalculator.Calculate(
                [
                    CreateLine(
                        quantity: 1m,
                        unitPrice: 10000m,
                        taxRate: 0m)
                ],
                [],
                DiscountType.FixedAmount,
                10000.01m));
    }

    [Fact]
    public void Calculate_FixedGeneralDiscount_AllocatesRoundingRemainder()
    {
        var result = SaleCalculator.Calculate(
            [
                CreateLine(1m, 10m, 0m),
                CreateLine(1m, 10m, 0m),
                CreateLine(1m, 10m, 0m)
            ],
            [],
            DiscountType.FixedAmount,
            1m);

        Assert.Equal(1m, result.GeneralDiscountTotal);
        Assert.Equal(29m, result.Total);

        Assert.Equal(
            1m,
            result.Lines.Sum(
                line => line.GeneralDiscountAmount));
    }

    private static SaleLineRequest CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        DiscountType? discountType = null,
        decimal discountValue = 0m)
    {
        return new SaleLineRequest
        {
            Description = "Test",
            Unit = "Unidad",
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate,
            DiscountType = discountType,
            DiscountValue = discountValue
        };
    }
}