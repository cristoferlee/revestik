using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerUpsertRequestValidationTests
{
    [Theory]
    [InlineData(IdentificationType.PhysicalPerson, "123456789")]
    [InlineData(IdentificationType.LegalEntity, "3101234567")]
    [InlineData(IdentificationType.Dimex, "12345678901")]
    [InlineData(IdentificationType.Dimex, "123456789012")]
    public void Validate_WithValidIdentification_ReturnsNoErrors(
        IdentificationType identificationType,
        string identificationNumber)
    {
        var request = CreateValidRequest(
            identificationType,
            identificationNumber);

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(IdentificationType.PhysicalPerson, "12345678")]
    [InlineData(IdentificationType.PhysicalPerson, "012345678")]
    [InlineData(IdentificationType.LegalEntity, "123456789")]
    [InlineData(IdentificationType.Dimex, "1234567890")]
    [InlineData(IdentificationType.Dimex, "01234567890")]
    public void Validate_WithInvalidIdentificationLengthOrPrefix_ReturnsError(
        IdentificationType identificationType,
        string identificationNumber)
    {
        var request = CreateValidRequest(
            identificationType,
            identificationNumber);

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.IdentificationNumber)));
    }

    [Fact]
    public void Validate_WithNonNumericIdentification_ReturnsError()
    {
        var request = CreateValidRequest(
            IdentificationType.PhysicalPerson,
            "1234A6789");

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.IdentificationNumber)));
    }

    [Fact]
    public void Validate_WithMissingRequiredValues_ReturnsErrors()
    {
        var request = new CustomerUpsertRequest();

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.Name)));

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.IdentificationType)));

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.IdentificationNumber)));

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.Email)));
    }

    [Fact]
    public void Validate_WithInvalidLocationCodes_ReturnsErrors()
    {
        var request = CreateValidRequest(
            IdentificationType.PhysicalPerson,
            "123456789");

        request.ProvinceCode = "8";
        request.CantonCode = "1";
        request.DistrictCode = "ABC";

        var validationResults = Validate(request);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.ProvinceCode)));

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.CantonCode)));

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(
                nameof(CustomerUpsertRequest.DistrictCode)));
    }

    private static CustomerUpsertRequest CreateValidRequest(
        IdentificationType identificationType,
        string identificationNumber)
    {
        return new CustomerUpsertRequest
        {
            Name = "Sample Customer",
            IdentificationType = identificationType,
            IdentificationNumber = identificationNumber,
            Email = "sample.customer@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Sample address details",
            IsActive = true
        };
    }

    private static IReadOnlyList<ValidationResult> Validate(
        CustomerUpsertRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);

        Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            validateAllProperties: true);

        return validationResults;
    }
}