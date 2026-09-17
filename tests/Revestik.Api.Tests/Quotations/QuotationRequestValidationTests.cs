using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationRequestValidationTests
{
    // -------------------------------------------------------------------------
    // QuotationLineRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void QuotationLine_WithValidValues_IsValid()
    {
        var request = CreateValidLine();

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void QuotationLine_WithMissingUnit_IsInvalid()
    {
        var request = CreateValidLine();
        request.Unit = string.Empty;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.Unit)));
    }

    [Fact]
    public void QuotationLine_WithUnitAboveMaximumLength_IsInvalid()
    {
        var request = CreateValidLine();
        request.Unit = new string('A', 51);

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.Unit)));
    }
    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("123456789012A")]
    public void QuotationLine_WithInvalidCabys_IsInvalid(string cabysCode)
    {
        var request = CreateValidLine();
        request.CabysCode = cabysCode;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.CabysCode)));
    }

    [Fact]
    public void QuotationLine_WithDescriptionAboveMaximumLength_IsInvalid()
    {
        var request = CreateValidLine();
        request.Description = new string('A', 501);

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.Description)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuotationLine_WithNonPositiveQuantity_IsInvalid(
        decimal quantity)
    {
        var request = CreateValidLine();
        request.Quantity = quantity;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.Quantity)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15000)]
    public void QuotationLine_WithNonPositiveUnitPrice_IsInvalid(
        decimal unitPrice)
    {
        var request = CreateValidLine();
        request.UnitPrice = unitPrice;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.UnitPrice)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void QuotationLine_WithSupportedTaxRate_IsValid(
        decimal taxRate)
    {
        var request = CreateValidLine();
        request.TaxRate = taxRate;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    public void QuotationLine_WithUnsupportedTaxRate_IsInvalid(
        decimal taxRate)
    {
        var request = CreateValidLine();
        request.TaxRate = taxRate;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.TaxRate)));
    }

    [Fact]
    public void QuotationLine_WithNegativeDiscount_IsInvalid()
    {
        var request = CreateValidLine();
        request.DiscountValue = -1m;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.DiscountValue)));
    }

    [Fact]
    public void QuotationLine_WithDiscountValueAndNoType_IsInvalid()
    {
        var request = CreateValidLine();
        request.DiscountType = null;
        request.DiscountValue = 1000m;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.DiscountType)));
    }

    [Fact]
    public void QuotationLine_WithDiscountTypeAndZeroValue_IsInvalid()
    {
        var request = CreateValidLine();
        request.DiscountType = DiscountType.FixedAmount;
        request.DiscountValue = 0m;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.DiscountValue)));
    }

    [Fact]
    public void QuotationLine_WithPercentageDiscountAboveOneHundred_IsInvalid()
    {
        var request = CreateValidLine();
        request.DiscountType = DiscountType.Percentage;
        request.DiscountValue = 101m;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.DiscountValue)));
    }

    [Fact]
    public void QuotationLine_WithValidPercentageDiscount_IsValid()
    {
        var request = CreateValidLine();
        request.DiscountType = DiscountType.Percentage;
        request.DiscountValue = 10m;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void QuotationLine_WithOneHundredPercentDiscount_IsValid()
    {
        var request = CreateValidLine();
        request.DiscountType = DiscountType.Percentage;
        request.DiscountValue = 100m;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void QuotationLine_WithFixedDiscountAboveGrossAmount_IsInvalid()
    {
        var request = CreateValidLine();
        request.Quantity = 2m;
        request.UnitPrice = 10000m;
        request.DiscountType = DiscountType.FixedAmount;
        request.DiscountValue = 20001m;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationLineRequest.DiscountValue)));
    }

    [Fact]
    public void QuotationLine_WithFixedDiscountEqualToGrossAmount_IsValid()
    {
        var request = CreateValidLine();
        request.Quantity = 2m;
        request.UnitPrice = 10000m;
        request.DiscountType = DiscountType.FixedAmount;
        request.DiscountValue = 20000m;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    // -------------------------------------------------------------------------
    // QuotationChargeRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void QuotationCharge_WithValidValues_IsValid()
    {
        var request = CreateValidCharge();

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void QuotationCharge_WithMissingDescription_IsInvalid()
    {
        var request = CreateValidCharge();
        request.Description = string.Empty;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationChargeRequest.Description)));
    }

    [Fact]
    public void QuotationCharge_WithDescriptionAboveMaximumLength_IsInvalid()
    {
        var request = CreateValidCharge();
        request.Description = new string('A', 501);

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationChargeRequest.Description)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuotationCharge_WithNonPositiveAmount_IsInvalid(
        decimal amount)
    {
        var request = CreateValidCharge();
        request.Amount = amount;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationChargeRequest.Amount)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void QuotationCharge_WithSupportedTaxRate_IsValid(
        decimal taxRate)
    {
        var request = CreateValidCharge();
        request.TaxRate = taxRate;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(14)]
    public void QuotationCharge_WithUnsupportedTaxRate_IsInvalid(
        decimal taxRate)
    {
        var request = CreateValidCharge();
        request.TaxRate = taxRate;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationChargeRequest.TaxRate)));
    }

    [Fact]
    public void QuotationCharge_WithInvalidType_IsInvalid()
    {
        var request = CreateValidCharge();
        request.Type = (QuotationChargeType)999;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationChargeRequest.Type)));
    }

    // -------------------------------------------------------------------------
    // QuotationUpsertRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void Quotation_WithValidValues_IsValid()
    {
        var request = CreateValidQuotation();

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quotation_WithInvalidCustomerId_IsInvalid(
        int customerId)
    {
        var request = CreateValidQuotation();
        request.CustomerId = customerId;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationUpsertRequest.CustomerId)));
    }

    [Fact]
    public void Quotation_WithInvalidCurrency_IsInvalid()
    {
        var request = CreateValidQuotation();
        request.Currency = (Currency)999;

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationUpsertRequest.Currency)));
    }

    [Fact]
    public void Quotation_WithNoLines_IsInvalid()
    {
        var request = CreateValidQuotation();
        request.Lines = [];

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationUpsertRequest.Lines)));
    }

    [Fact]
    public void Quotation_WithNoCharges_IsValid()
    {
        var request = CreateValidQuotation();
        request.Charges = [];

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void Quotation_WithPastValidityDate_IsInvalid()
    {
        var request = CreateValidQuotation();
        request.ValidUntilUtc = DateTime.UtcNow.AddDays(-1);

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(QuotationUpsertRequest.ValidUntilUtc)));
    }

    [Fact]
    public void Quotation_WithFutureValidityDate_IsValid()
    {
        var request = CreateValidQuotation();
        request.ValidUntilUtc = DateTime.UtcNow.AddDays(30);

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void Quotation_WithNoValidityDate_IsValid()
    {
        var request = CreateValidQuotation();
        request.ValidUntilUtc = null;

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static QuotationLineRequest CreateValidLine()
    {
        return new QuotationLineRequest
        {
            CabysCode = "1234567890123",
            Description = "Porcelanato 60x120",
            Unit = "m²",
            Quantity = 20m,
            UnitPrice = 15000m,
            DiscountType = null,
            DiscountValue = 0m,
            TaxRate = 13m
        };
    }

    private static QuotationChargeRequest CreateValidCharge()
    {
        return new QuotationChargeRequest
        {
            Type = QuotationChargeType.Transport,
            Description = "Delivery to project",
            Amount = 25000m,
            TaxRate = 13m
        };
    }

    private static QuotationUpsertRequest CreateValidQuotation()
    {
        return new QuotationUpsertRequest
        {
            CustomerId = 1,
            Currency = Currency.CRC,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            Lines =
            [
                CreateValidLine()
            ],
            Charges = []
        };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        return results;
    }
}