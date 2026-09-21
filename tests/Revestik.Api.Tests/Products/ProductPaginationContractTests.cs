using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductPaginationContractTests
{
    [Fact]
    public void ProductListRequest_WithDefaultValues_UsesFirstPageAndDefaultPageSize()
    {
        var request = new ProductListRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.Search);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void ProductListRequest_WithInvalidPagination_ReturnsValidationError(
        int page,
        int pageSize)
    {
        var request = new ProductListRequest
        {
            Page = page,
            PageSize = pageSize
        };

        var validationResults = Validate(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ProductListRequest_WithSearchOverMaximumLength_ReturnsValidationError()
    {
        var request = new ProductListRequest
        {
            Search = new string('a', 151)
        };

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(ProductListRequest.Search)));
    }

    [Fact]
    public void ProductListRequest_WithMaximumSearchLength_IsValid()
    {
        var request = new ProductListRequest
        {
            Search = new string('a', 150)
        };

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    private static IReadOnlyList<ValidationResult> Validate(
        ProductListRequest request)
    {
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        return validationResults;
    }
}