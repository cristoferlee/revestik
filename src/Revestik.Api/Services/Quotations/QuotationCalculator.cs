using Revestik.Api.Services.Commercial;
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

        var calculation =
            CommercialCalculator.CalculateLine(
                line.Quantity,
                line.UnitPrice,
                discountAmount,
                line.TaxRate);

        return new QuotationLineCalculation(
            calculation.BaseAmount,
            calculation.DiscountAmount,
            calculation.TaxAmount,
            calculation.TotalAmount);
    }
}

public sealed record QuotationLineCalculation(
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount);