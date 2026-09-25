using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class InventoryCatalogRequestValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Category_WithEmptyName_IsInvalid(string name)
    {
        var request = new ProductCategoryUpsertRequest
        {
            Name = name
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.Name)));
    }

    [Fact]
    public void Category_WithNameOverMaximumLength_IsInvalid()
    {
        var request = new ProductCategoryUpsertRequest
        {
            Name = new string('a', 101)
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Unit_WithValidValues_IsValid()
    {
        var request = new UnitOfMeasureUpsertRequest
        {
            Name = "Metro cuadrado",
            Symbol = "m²"
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("", "m²")]
    [InlineData("Metro cuadrado", "")]
    [InlineData("   ", "m²")]
    [InlineData("Metro cuadrado", "   ")]
    public void Unit_WithEmptyRequiredValue_IsInvalid(
        string name,
        string symbol)
    {
        var request = new UnitOfMeasureUpsertRequest
        {
            Name = name,
            Symbol = symbol
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Unit_WithValuesOverMaximumLength_IsInvalid()
    {
        var request = new UnitOfMeasureUpsertRequest
        {
            Name = new string('a', 101),
            Symbol = new string('b', 21)
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(request.Name)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(request.Symbol)));
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