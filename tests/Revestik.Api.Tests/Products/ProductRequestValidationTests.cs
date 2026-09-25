using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductRequestValidationTests
{
    [Fact]
    public void ValidRequest_HasNoValidationErrors()
    {
        Assert.Empty(Validate(CreateValidRequest()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345678901234")]
    [InlineData("123456789012A")]
    public void InvalidCabys_ReturnsValidationError(string cabysCode)
    {
        var request = CreateValidRequest();
        request.CabysCode = cabysCode;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.CabysCode)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCatalogId_ReturnsValidationError(int id)
    {
        var request = CreateValidRequest();
        request.CategoryId = id;
        request.InventoryUnitId = id;
        request.CommercialUnitId = id;

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(request.CategoryId)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(request.InventoryUnitId)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(request.CommercialUnitId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveConversion_ReturnsValidationError(
        decimal conversion)
    {
        var request = CreateValidRequest();
        request.CommercialUnitsPerInventoryUnit = conversion;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.CommercialUnitsPerInventoryUnit)));
    }

    [Fact]
    public void SameUnit_WithConversionOtherThanOne_ReturnsValidationError()
    {
        var request = CreateValidRequest();
        request.CommercialUnitId = request.InventoryUnitId;
        request.CommercialUnitsPerInventoryUnit = 1.44m;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(
                nameof(request.CommercialUnitsPerInventoryUnit)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(14)]
    public void UnsupportedTaxRate_ReturnsValidationError(decimal taxRate)
    {
        var request = CreateValidRequest();
        request.TaxRate = taxRate;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.TaxRate)));
    }

    [Fact]
    public void NegativeMinimumStock_ReturnsValidationError()
    {
        var request = CreateValidRequest();
        request.MinimumStock = -1m;

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.MinimumStock)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void NonPositivePriceOrCost_ReturnsValidationError(
        decimal salePrice,
        decimal currentCost)
    {
        var request = CreateValidRequest();
        request.SalePrice = salePrice;
        request.CurrentCost = currentCost;

        Assert.NotEmpty(Validate(request));
    }

    private static ProductUpsertRequest CreateValidRequest()
    {
        return new ProductUpsertRequest
        {
            CategoryId = 1,
            Name = "Porcelanato Blanco 60x120",
            Description = "Porcelanato rectificado.",
            CabysCode = "1234567890123",
            InventoryUnitId = 1,
            CommercialUnitId = 2,
            CommercialUnitsPerInventoryUnit = 1.44m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            MinimumStock = 5m
        };
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