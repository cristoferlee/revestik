using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Common;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerPaginationContractTests
{
    [Fact]
    public void CustomerListRequest_WithDefaultValues_UsesFirstPageAndDefaultPageSize()
    {
        var request = new CustomerListRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.Search);
        Assert.Null(request.IdentificationType);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(40, 2)]
    public void PaginatedResponse_WithTotalCount_CalculatesTotalPages(
        int totalCount,
        int expectedTotalPages)
    {
        var response = new PaginatedResponse<int>(
            [],
            Page: 1,
            PageSize: 20,
            TotalCount: totalCount);

        Assert.Equal(expectedTotalPages, response.TotalPages);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void CustomerListRequest_WithInvalidPagination_ReturnsValidationError(
        int page,
        int pageSize)
    {
        var request = new CustomerListRequest
        {
            Page = page,
            PageSize = pageSize
        };

        var validationResults = Validate(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void CustomerListRequest_WithSearchOverMaximumLength_ReturnsValidationError()
    {
        var request = new CustomerListRequest
        {
            Search = new string('a', 151)
        };

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerListRequest.Search)));
    }

    [Fact]
    public void CustomerListRequest_WithUndefinedIdentificationType_ReturnsValidationError()
    {
        var request = new CustomerListRequest
        {
            IdentificationType = (IdentificationType)999
        };

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerListRequest.IdentificationType)));
    }

    private static IReadOnlyList<ValidationResult> Validate(
        CustomerListRequest request)
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