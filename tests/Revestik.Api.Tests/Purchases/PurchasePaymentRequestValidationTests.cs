using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchasePaymentRequestValidationTests
{
    [Fact]
    public void CashPurchase_WithoutInitialPayment_IsInvalid()
    {
        var request = CreateBasePurchase();
        request.PaymentType = PurchasePaymentType.Cash;
        request.InitialPayment = null;

        Assert.Contains(
            Validate(request),
            result =>
                result.MemberNames.Contains(
                    nameof(request.InitialPayment)));
    }

    [Fact]
    public void CreditPurchase_WithoutTerm_IsInvalid()
    {
        var request = CreateBasePurchase();
        request.PaymentType = PurchasePaymentType.Credit;
        request.CreditTermDays = null;
        request.InitialPayment = null;

        Assert.Contains(
            Validate(request),
            result =>
                result.MemberNames.Contains(
                    nameof(request.CreditTermDays)));
    }

    [Fact]
    public void Payment_WithInvalidExchangeRate_IsInvalid()
    {
        var request = new PurchasePaymentRequest
        {
            Amount = 100m,
            PaymentMethod = PaymentMethod.BankTransfer,
            PaidAtUtc = DateTime.UtcNow,
            ExchangeRate = 0m
        };

        Assert.Contains(
            Validate(request),
            result =>
                result.MemberNames.Contains(
                    nameof(request.ExchangeRate)));
    }

    private static PurchaseCreateRequest CreateBasePurchase() =>
        new()
        {
            SupplierId = 1,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.Today),
            Currency = PurchaseCurrency.CRC,
            Lines =
            [
                new PurchaseLineRequest
                {
                    ProductId = 1,
                    Quantity = 1m,
                    UnitCost = 1000m
                }
            ]
        };

    private static IReadOnlyList<ValidationResult>
        Validate(object request)
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