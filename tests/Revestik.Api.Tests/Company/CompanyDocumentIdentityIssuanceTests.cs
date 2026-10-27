using Revestik.Api.Models;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyDocumentIdentityIssuanceTests
{
    [Fact]
    public void CapturedSnapshot_DoesNotChangeAfterCompanyProfileIsEdited()
    {
        var profile = NewProfile();
        var serialized = CompanyDocumentIdentitySnapshotSerializer.Serialize(
            CompanyDocumentIdentityResolver.Capture(profile));

        profile.CommercialName = "Nuevo nombre comercial";
        profile.Email = "new@example.com";
        profile.LogoStorageKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        var restored = CompanyDocumentIdentitySnapshotSerializer.Deserialize(serialized);
        Assert.NotNull(restored);
        Assert.Equal("Nombre original", restored.CommercialName);
        Assert.Equal("original@example.com", restored.Email);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", restored.LogoStorageKey);
    }

    [Fact]
    public void DefaultLogo_IsExplicitlyRepresentedByNullStorageKey()
    {
        var profile = NewProfile();
        profile.LogoStorageKey = null;
        profile.LogoContentType = null;

        var serialized = CompanyDocumentIdentitySnapshotSerializer.Serialize(
            CompanyDocumentIdentityResolver.Capture(profile));
        var restored = CompanyDocumentIdentitySnapshotSerializer.Deserialize(serialized);

        Assert.NotNull(restored);
        Assert.Null(restored.LogoStorageKey);
        Assert.Null(restored.LogoContentType);
    }

    private static CompanyProfile NewProfile() => new()
    {
        LegalName = "Empresa original S.A.",
        IdentificationNumber = "3101000000",
        CommercialName = "Nombre original",
        Email = "original@example.com",
        LogoStorageKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        LogoContentType = "image/png"
    };
}
