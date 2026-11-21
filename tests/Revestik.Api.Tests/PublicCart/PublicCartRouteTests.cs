using Revestik.Public;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicCartRouteTests
{
    [Theory]
    [InlineData("es", "/es/carrito")]
    [InlineData("en", "/en/cart")]
    [InlineData("de", "/de/warenkorb")]
    public void CartPathsAreLocalizedAndRemainOutsideEditorialRoutes(string language, string path)
    {
        Assert.Equal(path, SiteLinks.CartPath(language));
        Assert.True(SiteLinks.IsCartRoute("https://localhost:7126" + path));
        Assert.False(SiteLinks.IsKnownPublicRoute("https://localhost:7126" + path));
        Assert.Equal(-1, SiteLinks.PageFromUri("https://localhost:7126" + path));
    }

    [Theory]
    [InlineData("/es/carrito", "en", "/en/cart")]
    [InlineData("/en/cart", "de", "/de/warenkorb")]
    [InlineData("/de/warenkorb", "es", "/es/carrito")]
    public void LanguageSwitchPreservesCartRatherThanSwitchingToHome(
        string originalPath, string language, string expectedPath)
    {
        Assert.Equal(expectedPath,
            SiteLinks.LanguagePath(language, "https://localhost:7126" + originalPath));
    }

    [Theory]
    [InlineData("/es/carrito/extra")]
    [InlineData("/en/warenkorb")]
    [InlineData("/de/cart")]
    public void InvalidCartPathsAreNotRecognized(string path)
    {
        Assert.False(SiteLinks.IsCartRoute("https://localhost:7126" + path));
    }
}
