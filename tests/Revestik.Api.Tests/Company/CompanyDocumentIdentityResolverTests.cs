using Revestik.Api.Configuration;
using Revestik.Api.Models;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyDocumentIdentityResolverTests
{
    private static CompanyOptions Original() => new()
    {
        LegalName = "Original", IdentificationNumber = "123", Website = "original.test"
    };

    private static CompanyProfile Current() => new()
    {
        LegalName = "Actual", IdentificationNumber = "456", CommercialName = "Tienda",
        Website = "actual.test", LogoStorageKey = new string('a', 32),
        LogoContentType = "image/png"
    };

    [Fact]
    public void DraftUsesCurrentProfile()
    {
        var current = Current();
        var identity = CompanyDocumentIdentityResolver.Resolve(false, null, current, Original());
        Assert.Equal("Actual", identity.LegalName);
        Assert.Equal(current.LogoStorageKey, identity.LogoStorageKey);
    }

    [Fact]
    public void IssuedUsesCapturedIdentityEvenAfterProfileChanges()
    {
        var current = Current();
        var snapshot = CompanyDocumentIdentityResolver.Capture(current);
        current.LegalName = "Cambiado";
        current.LogoStorageKey = new string('b', 32);
        var identity = CompanyDocumentIdentityResolver.Resolve(true, snapshot, current, Original());
        Assert.Equal("Actual", identity.LegalName);
        Assert.Equal(new string('a', 32), identity.LogoStorageKey);
    }

    [Fact]
    public void IssuedLegacyUsesOriginalConfigurationNotCurrentProfile()
    {
        var identity = CompanyDocumentIdentityResolver.Resolve(true, null, Current(), Original());
        Assert.Equal("Original", identity.LegalName);
        Assert.Null(identity.LogoStorageKey);
    }

    [Fact]
    public void DefaultLogoAtIssuanceDoesNotFollowLaterCustomLogo()
    {
        var profile = Current();
        profile.LogoStorageKey = null;
        profile.LogoContentType = null;
        var snapshot = CompanyDocumentIdentityResolver.Capture(profile);
        profile.LogoStorageKey = new string('c', 32);
        Assert.Null(CompanyDocumentIdentityResolver.Resolve(true, snapshot, profile, Original()).LogoStorageKey);
    }

    [Fact]
    public void DraftWithoutInitializedProfileUsesOriginalConfiguration()
    {
        var identity = CompanyDocumentIdentityResolver.Resolve(false, null, null, Original());
        Assert.Equal("Original", identity.LegalName);
    }
}
