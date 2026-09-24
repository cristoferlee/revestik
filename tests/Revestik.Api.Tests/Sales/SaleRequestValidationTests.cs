using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleRequestValidationTests
{
    [Fact]
    public void SaleUpsertRequest_WithValidData_IsValid()
    {
        var request = CreateValidSaleRequest();

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Fact]
    public void SaleUpsertRequest_WithoutCustomer_IsInvalid()
    {
        var request = CreateValidSaleRequest();
        request.CustomerId = 0;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleUpsertRequest_WithoutLines_IsInvalid()
    {
        var request = CreateValidSaleRequest();
        request.Lines = [];

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleUpsertRequest_WithDiscountValueButNoType_IsInvalid()
    {
        var request = CreateValidSaleRequest();
        request.GeneralDiscountType = null;
        request.GeneralDiscountValue = 10m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleUpsertRequest_WithDiscountTypeButZeroValue_IsInvalid()
    {
        var request = CreateValidSaleRequest();
        request.GeneralDiscountType = DiscountType.Percentage;
        request.GeneralDiscountValue = 0m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleUpsertRequest_WithPercentageOverOneHundred_IsInvalid()
    {
        var request = CreateValidSaleRequest();
        request.GeneralDiscountType = DiscountType.Percentage;
        request.GeneralDiscountValue = 100.01m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleLineRequest_WithInvalidQuantity_IsInvalidThroughParent()
    {
        var request = CreateValidSaleRequest();
        request.Lines[0].Quantity = 0m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleLineRequest_WithInvalidTaxRate_IsInvalidThroughParent()
    {
        var request = CreateValidSaleRequest();
        request.Lines[0].TaxRate = 7m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleLineRequest_WithFixedDiscountAboveGross_IsInvalidThroughParent()
    {
        var request = CreateValidSaleRequest();

        request.Lines[0].DiscountType =
            DiscountType.FixedAmount;

        request.Lines[0].DiscountValue =
            101m;

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SaleChargeRequest_OtherWithoutDescription_IsInvalidThroughParent()
    {
        var request = CreateValidSaleRequest();

        request.Charges =
        [
            new SaleChargeRequest
            {
                Type = SaleChargeType.Other,
                Description = string.Empty,
                Amount = 10m
            }
        ];

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void SalePaymentRequest_WithValidData_IsValid()
    {
        var request = new SalePaymentRequest
        {
            Amount = 100m,
            PaymentMethod = PaymentMethod.Sinpe,
            PaidAtUtc = DateTime.UtcNow,
            Reference = "SINPE-TEST",
            Notes = "Pago de prueba."
        };

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Fact]
    public void SalePaymentRequest_WithZeroAmount_IsInvalid()
    {
        var request = new SalePaymentRequest
        {
            Amount = 0m,
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow
        };

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    public void VoidSaleRequest_WithReasonShorterThanThreeCharacters_IsInvalid(
        string reason)
    {
        var request = new VoidSaleRequest
        {
            Reason = reason
        };

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void VoidSaleRequest_WithValidReason_IsValid()
    {
        var request = new VoidSaleRequest
        {
            Reason = "Corrección requerida."
        };

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    public void VoidSalePaymentRequest_WithReasonShorterThanThreeCharacters_IsInvalid(
        string reason)
    {
        var request = new VoidSalePaymentRequest
        {
            Reason = reason
        };

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void VoidSalePaymentRequest_WithValidReason_IsValid()
    {
        var request = new VoidSalePaymentRequest
        {
            Reason = "Pago registrado incorrectamente."
        };

        var results = Validate(request);

        Assert.Empty(results);
    }

    private static SaleUpsertRequest CreateValidSaleRequest()
    {
        return new SaleUpsertRequest
        {
            CustomerId = 1,
            Currency = Currency.CRC,
            GeneralDiscountType = null,
            GeneralDiscountValue = 0m,
            Observations = string.Empty,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = null,
                    CabysCode = string.Empty,
                    Description = "Producto de prueba",
                    Unit = "Unidad",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountType = null,
                    DiscountValue = 0m,
                    TaxRate = 13m
                }
            ],
            Charges = []
        };
    }

    private static IReadOnlyList<ValidationResult> Validate<T>(
        T model)
        where T : class
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