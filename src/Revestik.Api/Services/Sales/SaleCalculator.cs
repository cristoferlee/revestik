using Revestik.Api.Services.Commercial;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales;

public static class SaleCalculator
{
    public static SaleCalculation Calculate(
        IReadOnlyList<SaleLineRequest> lines,
        IReadOnlyList<SaleChargeRequest> charges,
        DiscountType? generalDiscountType,
        decimal generalDiscountValue)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(charges);

        if (lines.Count == 0)
        {
            throw new ArgumentException(
                "A sale must contain at least one line.",
                nameof(lines));
        }

        var initialLines = lines
            .Select(CalculateBeforeGeneralDiscount)
            .ToList();

        var lineTotalBeforeGeneralDiscount =
            CommercialCalculator.Round(
                initialLines.Sum(line => line.TotalAmount));

        var generalDiscountAmount =
            CalculateGeneralDiscountAmount(
                lineTotalBeforeGeneralDiscount,
                generalDiscountType,
                generalDiscountValue);

        var allocations = AllocateGeneralDiscount(
            initialLines,
            generalDiscountAmount,
            lineTotalBeforeGeneralDiscount);

        var finalLines =
            new List<SaleLineCalculation>(lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            var source = lines[index];
            var initial = initialLines[index];
            var generalDiscountAllocation =
                allocations[index];

            var finalTaxInclusiveAmount =
                initial.TotalAmount -
                generalDiscountAllocation;

            var finalAmounts =
                CommercialCalculator
                    .CalculateFromTaxInclusiveAmount(
                        finalTaxInclusiveAmount,
                        initial.LineDiscountAmount +
                        generalDiscountAllocation,
                        source.TaxRate);

            finalLines.Add(
                new SaleLineCalculation(
                    BaseAmount: finalAmounts.BaseAmount,
                    LineDiscountAmount:
                        initial.LineDiscountAmount,
                    GeneralDiscountAmount:
                        generalDiscountAllocation,
                    TaxAmount: finalAmounts.TaxAmount,
                    TotalAmount: finalAmounts.TotalAmount));
        }

        var lineDiscountTotal =
            CommercialCalculator.Round(
                finalLines.Sum(
                    line => line.LineDiscountAmount));

        var actualGeneralDiscountTotal =
            CommercialCalculator.Round(
                finalLines.Sum(
                    line => line.GeneralDiscountAmount));

        var subtotal =
            CommercialCalculator.Round(
                finalLines.Sum(line => line.BaseAmount));

        var taxTotal =
            CommercialCalculator.Round(
                finalLines.Sum(line => line.TaxAmount));

        var chargeTotal =
            CommercialCalculator.Round(
                charges.Sum(charge => charge.Amount));

        var total =
            CommercialCalculator.Round(
                finalLines.Sum(line => line.TotalAmount) +
                chargeTotal);

        return new SaleCalculation(
            Lines: finalLines,
            Subtotal: subtotal,
            LineDiscountTotal: lineDiscountTotal,
            GeneralDiscountTotal:
                actualGeneralDiscountTotal,
            DiscountTotal:
                CommercialCalculator.Round(
                    lineDiscountTotal +
                    actualGeneralDiscountTotal),
            TaxTotal: taxTotal,
            ChargeTotal: chargeTotal,
            Total: total);
    }

    private static InitialSaleLineCalculation
        CalculateBeforeGeneralDiscount(
            SaleLineRequest line)
    {
        var grossAmount =
            line.Quantity * line.UnitPrice;

        var lineDiscountAmount =
            line.DiscountType switch
            {
                DiscountType.Percentage =>
                    grossAmount *
                    (line.DiscountValue / 100m),

                DiscountType.FixedAmount =>
                    line.DiscountValue,

                _ => 0m
            };

        var calculation =
            CommercialCalculator.CalculateLine(
                line.Quantity,
                line.UnitPrice,
                lineDiscountAmount,
                line.TaxRate);

        return new InitialSaleLineCalculation(
            LineDiscountAmount:
                calculation.DiscountAmount,
            TotalAmount:
                calculation.TotalAmount);
    }

    private static decimal CalculateGeneralDiscountAmount(
        decimal lineTotal,
        DiscountType? discountType,
        decimal discountValue)
    {
        if (discountType is null)
        {
            if (discountValue != 0m)
            {
                throw new ArgumentException(
                    "A general discount type is required when the discount value is greater than zero.");
            }

            return 0m;
        }

        if (discountValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountValue),
                "The general discount value must be greater than zero.");
        }

        var amount = discountType switch
        {
            DiscountType.Percentage
                when discountValue <= 100m =>
                    lineTotal *
                    (discountValue / 100m),

            DiscountType.Percentage =>
                throw new ArgumentOutOfRangeException(
                    nameof(discountValue),
                    "The general percentage discount cannot exceed 100%."),

            DiscountType.FixedAmount =>
                discountValue,

            _ => throw new ArgumentOutOfRangeException(
                nameof(discountType),
                "The general discount type is not valid.")
        };

        var roundedAmount =
            CommercialCalculator.Round(amount);

        if (roundedAmount > lineTotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountValue),
                "The general discount cannot exceed the total amount of the sale lines.");
        }

        return roundedAmount;
    }

    private static IReadOnlyList<decimal>
        AllocateGeneralDiscount(
            IReadOnlyList<InitialSaleLineCalculation> lines,
            decimal generalDiscountAmount,
            decimal lineTotal)
    {
        var allocations =
            new decimal[lines.Count];

        if (generalDiscountAmount == 0m)
        {
            return allocations;
        }

        var allocated = 0m;

        for (var index = 0;
             index < lines.Count - 1;
             index++)
        {
            var proportion =
                lineTotal == 0m
                    ? 0m
                    : lines[index].TotalAmount /
                      lineTotal;

            allocations[index] =
                CommercialCalculator.Round(
                    generalDiscountAmount *
                    proportion);

            allocated += allocations[index];
        }

        allocations[^1] =
            CommercialCalculator.Round(
                generalDiscountAmount - allocated);

        return allocations;
    }

    private sealed record InitialSaleLineCalculation(
        decimal LineDiscountAmount,
        decimal TotalAmount);
}

public sealed record SaleLineCalculation(
    decimal BaseAmount,
    decimal LineDiscountAmount,
    decimal GeneralDiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount);

public sealed record SaleCalculation(
    IReadOnlyList<SaleLineCalculation> Lines,
    decimal Subtotal,
    decimal LineDiscountTotal,
    decimal GeneralDiscountTotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ChargeTotal,
    decimal Total);