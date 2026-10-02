using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Tests.Suppliers;

public sealed class SupplierRequestValidationTests
{
    [Fact]
    public void ValidSupplier_IsValid()
    {
        var request = CreateValidRequest();

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("", "Contacto", "88888888", "test@example.com")]
    [InlineData("Proveedor", "", "88888888", "test@example.com")]
    [InlineData("Proveedor", "Contacto", "", "test@example.com")]
    [InlineData("Proveedor", "Contacto", "88888888", "")]
    public void MissingRequiredValue_IsInvalid(
        string name,
        string contactName,
        string phone,
        string email)
    {
        var request = new SupplierUpsertRequest
        {
            Name = name,
            ContactName = contactName,
            PhoneNumber = phone,
            Email = email
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void InvalidEmail_IsRejected()
    {
        var request = CreateValidRequest();
        request.Email = "not-an-email";

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.Email)));
    }

    private static SupplierUpsertRequest CreateValidRequest() =>
        new()
        {
            Name = "Proveedor Test",
            ContactName = "Contacto Test",
            PhoneNumber = "88888888",
            Email = "proveedor@example.com"
        };

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