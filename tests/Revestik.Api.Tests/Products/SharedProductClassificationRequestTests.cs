using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class SharedProductClassificationRequestTests
{
    [Fact]
    public void SubcategoryIsOptionalForExistingProductContracts()
    {
        var request = new ProductUpsertRequest { CategoryId = 4 };
        Assert.Null(request.SubcategoryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSubcategoryIdentifiersAreRejected(int id)
    {
        var request = new ProductUpsertRequest { CategoryId = 4, SubcategoryId = id };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.SubcategoryId)));
    }
}
