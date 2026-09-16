using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations;

public static class QuotationCalculator
{
    public static QuotationLineCalculation CalculateLine(
        QuotationLineRequest line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var grossAmount = line.Quantity * line.UnitPrice;

        var discountAmount = line.DiscountType switch
        {
            DiscountType.Percentage =>
                grossAmount * (line.DiscountValue / 100m),

            DiscountType.FixedAmount =>
                line.DiscountValue,

            _ => 0m
        };

        var amountAfterDiscount = grossAmount - discountAmount;

        var baseAmount = line.TaxRate switch
        {
            13m => amountAfterDiscount / 1.13m,
            0m => amountAfterDiscount / 1.13m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(line),
                "Tax rate must be 0% or 13%.")
        };

        var taxAmount = line.TaxRate == 13m
            ? amountAfterDiscount - baseAmount
            : 0m;

        var totalAmount = baseAmount + taxAmount;

        return new QuotationLineCalculation(
            BaseAmount: Round(baseAmount),
            DiscountAmount: Round(discountAmount),
            TaxAmount: Round(taxAmount),
            TotalAmount: Round(totalAmount));
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}

public sealed record QuotationLineCalculation(
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount);