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
        Assert.Null(request.CategoryId);
        Assert.Null(request.UnitId);
        Assert.Equal(ProductActivityStatus.Active, request.ActivityStatus);
        Assert.Equal(ProductStockStatus.All, request.StockStatus);
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

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    public void ProductListRequest_WithInvalidCatalogFilter_ReturnsValidationError(
        int? categoryId,
        int? unitId)
    {
        var request = new ProductListRequest
        {
            CategoryId = categoryId,
            UnitId = unitId
        };

        Assert.NotEmpty(Validate(request));
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