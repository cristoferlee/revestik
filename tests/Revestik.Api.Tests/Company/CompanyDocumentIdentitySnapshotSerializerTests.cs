using System.IO;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyDocumentIdentitySnapshotSerializerTests
{
    [Fact]
    public void RoundTripPreservesAllFieldsAndHistoricalLogoReference()
    {
        var input = new CompanyDocumentIdentity
        {
            LegalName = "Razón original", IdentificationNumber = "3101000000",
            TaxIdentificationType = "02", TaxIdentificationNumber = "3101000000",
            CommercialName = "Comercial", PhoneNumber = "22222222",
            Email = "contacto@example.test", Website = "example.test",
            Address = "Heredia", ProvinceCode = "4", CantonCode = "01",
            DistrictCode = "01", OtherSigns = "Frente al parque",
            LogoStorageKey = new string('a', 32), LogoContentType = "image/png"
        };
        var restored = CompanyDocumentIdentitySnapshotSerializer.Deserialize(
            CompanyDocumentIdentitySnapshotSerializer.Serialize(input));
        Assert.Equal(input, restored);
    }

    [Fact]
    public void NullMeansLegacyDocument()
    {
        Assert.Null(CompanyDocumentIdentitySnapshotSerializer.Deserialize(null));
    }

    [Fact]
    public void DefaultLogoRemainsExplicitlyAbsentAfterRoundTrip()
    {
        var input = new CompanyDocumentIdentity
        { LegalName = "Revestik", IdentificationNumber = "123" };
        var output = CompanyDocumentIdentitySnapshotSerializer.Deserialize(
            CompanyDocumentIdentitySnapshotSerializer.Serialize(input));
        Assert.NotNull(output);
        Assert.Null(output.LogoStorageKey);
        Assert.Null(output.LogoContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"legalName\":\"Company\"}")]
    public void InvalidOrIncompleteSnapshotFailsExplicitly(string json)
    {
        Assert.Throws<InvalidDataException>(() =>
            CompanyDocumentIdentitySnapshotSerializer.Deserialize(json));
    }
}
