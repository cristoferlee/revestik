using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseRequestValidationTests
{
    [Fact]
    public void CrcPurchase_WithoutExchangeRate_IsValid()
    {
        var request = CreateValidRequest();

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void UsdPurchase_WithExchangeRate_IsValid()
    {
        var request = CreateValidRequest();
        request.Currency = PurchaseCurrency.USD;
        request.ExchangeRate = 505.25m;

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void UsdPurchase_WithoutExchangeRate_IsInvalid()
    {
        var request = CreateValidRequest();
        request.Currency = PurchaseCurrency.USD;
        request.ExchangeRate = null;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.ExchangeRate)));
    }

    [Fact]
    public void CrcPurchase_WithExchangeRate_IsInvalid()
    {
        var request = CreateValidRequest();
        request.ExchangeRate = 505.25m;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.ExchangeRate)));
    }

    [Fact]
    public void Purchase_WithoutDate_IsInvalid()
    {
        var request = CreateValidRequest();
        request.PurchaseDate = null;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.PurchaseDate)));
    }

    [Fact]
    public void Purchase_WithoutLines_IsInvalid()
    {
        var request = CreateValidRequest();
        request.Lines = [];

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.Lines)));
    }

    [Fact]
    public void PurchaseLine_WithMoreThanFourQuantityDecimals_IsInvalid()
    {
        var request = CreateValidRequest();
        request.Lines[0].Quantity = 1.00001m;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void PurchaseLine_WithMoreThanTwoCostDecimals_IsInvalid()
    {
        var request = CreateValidRequest();
        request.Lines[0].UnitCost = 1000.001m;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void PurchaseLine_WithoutProduct_IsInvalid()
    {
        var request = CreateValidRequest();
        request.Lines[0].ProductId = 0;

        Assert.NotEmpty(Validate(request));
    }

    private static PurchaseCreateRequest CreateValidRequest()
    {
        return new PurchaseCreateRequest
        {
            SupplierId = 1,
            PurchaseDate = new DateOnly(2026, 10, 1),
            Currency = PurchaseCurrency.CRC,
            PaymentType = PurchasePaymentType.Cash,
            InitialPayment = new PurchasePaymentRequest
            {
                Amount = 1000m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAtUtc = DateTime.UtcNow
            },
            Notes = "Compra para bodega.",
            Lines =
            [
                new PurchaseLineRequest
                {
                    ProductId = 1,
                    Quantity = 10m,
                    UnitCost = 5000m
                }
            ]
        };
    }


    [Fact]
    public void Purchase_WithDuplicateProduct_IsInvalid()
    {
        var request = CreateValidRequest();

        request.Lines.Add(
            new PurchaseLineRequest
            {
                ProductId = request.Lines[0].ProductId,
                Quantity = 1m,
                UnitCost = 1000m
            });

        Assert.Contains(
            Validate(request),
            result =>
                result.MemberNames.Contains(
                    nameof(request.Lines)));
    }


    private static IReadOnlyList<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        return results;
    }
}