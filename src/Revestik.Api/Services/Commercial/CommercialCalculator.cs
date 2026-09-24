namespace Revestik.Api.Services.Commercial;

public static class CommercialCalculator
{
    public static CommercialLineCalculation CalculateLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal taxRate)
    {
        var grossAmount = quantity * unitPrice;

        if (discountAmount < 0m ||
            discountAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                "Discount amount must be between zero and the gross amount.");
        }

        var amountAfterDiscount =
            grossAmount - discountAmount;

        return CalculateFromTaxInclusiveAmount(
            amountAfterDiscount,
            discountAmount,
            taxRate);
    }

    public static CommercialLineCalculation
        CalculateFromTaxInclusiveAmount(
            decimal taxInclusiveAmount,
            decimal discountAmount,
            decimal taxRate)
    {
        if (taxInclusiveAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxInclusiveAmount),
                "Tax-inclusive amount cannot be negative.");
        }

        var baseAmount = taxRate switch
        {
            13m => taxInclusiveAmount / 1.13m,
            0m => taxInclusiveAmount,
            _ => throw new ArgumentOutOfRangeException(
                nameof(taxRate),
                "Tax rate must be 0% or 13%.")
        };

        var taxAmount = taxRate == 13m
            ? taxInclusiveAmount - baseAmount
            : 0m;

        return new CommercialLineCalculation(
            BaseAmount: Round(baseAmount),
            DiscountAmount: Round(discountAmount),
            TaxAmount: Round(taxAmount),
            TotalAmount: Round(taxInclusiveAmount));
    }

    public static decimal Round(decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}

public sealed record CommercialLineCalculation(
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount);