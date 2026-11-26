using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Revestik.Api.Services.Authentication;
using Revestik.Public;
using System.Security.Claims;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicAuthenticationReturnPathTests
{
    [Theory]
    [InlineData("/es", "es", "/es")]
    [InlineData("/es/acabados", "es", "/es/acabados")]
    [InlineData("/en/projects/hotel-example", "en", "/en/projects/hotel-example")]
    [InlineData("/de/oberflaechen/kategorie/fliesen", "de", "/de/oberflaechen/kategorie/fliesen")]
    [InlineData("/es/acabados/porcelanato", "es", "/es/acabados/porcelanato")]
    [InlineData("/es/carrito", "es", "/es/carrito")]
    [InlineData("/en/cart", "en", "/en/cart")]
    [InlineData("/de/warenkorb", "de", "/de/warenkorb")]
    [InlineData("/es/acabados?categoryId=12", "es", "/es/acabados?categoryId=12")]
    [InlineData("/es/acabados?categoryId=12&editor=true", "es", "/es/acabados?categoryId=12")]
    [InlineData("/en/projects?page=2&authError=test", "en", "/en/projects?page=2")]
    [InlineData("/es/acabados?categoryId=-1", "es", "/es/acabados")]
    [InlineData("/es/acabados?categoryId=1&categoryId=2", "es", "/es/acabados")]
    [InlineData("/es/acabados?authError=old", "es", "/es/acabados")]
    [InlineData("https://attacker.example", "es", "/es")]
    [InlineData("//attacker.example", "es", "/es")]
    [InlineData("/\\attacker.example", "es", "/es")]
    [InlineData("/administracion", "en", "/en")]
    [InlineData("/api/auth/me", "de", "/de")]
    [InlineData("/es/../administracion", "es", "/es")]
    [InlineData("/es/%2e%2e/administracion", "es", "/es")]
    [InlineData("/es/acabados/%2f%2fexample", "es", "/es")]
    [InlineData("/es//acabados", "es", "/es")]
    [InlineData("/es/acabados\r\nLocation: evil", "es", "/es")]
    [InlineData("/es/acabados#fragment", "es", "/es")]
    [InlineData("/es/unknown", "es", "/es")]
    [InlineData("/en/finishes/categoria/test", "en", "/en")]
    [InlineData("/en/cart/unexpected", "en", "/en")]
    [InlineData("/de/warenkorb", "es", "/es")]
    [InlineData(null, "de", "/de")]
    public void ReturnPath_IsStrictlyRestricted(
        string? candidate, string language, string expected)
    {
        Assert.Equal(expected,
            SiteLinks.NormalizeAuthenticationReturnPath(
                candidate, language));
    }

    [Fact]
    public void ProtectedState_PreservesValidatedDestination()
    {
        var properties = new AuthenticationProperties();

        GoogleLoginFlow.SetIntent(
            properties, GoogleLoginFlow.Public);

        GoogleLoginFlow.SetPublicLanguage(properties, "es");

        GoogleLoginFlow.SetPublicReturnPath(
            properties,
            "/es/acabados?categoryId=12",
            "es");

        var login = new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity([], "Google")),
            "Google", "test-subject", "Google")
        {
            AuthenticationProperties = properties
        };

        Assert.Equal(
            "/es/acabados?categoryId=12",
            GoogleLoginFlow.GetPublicReturnPath(login));

        properties.Items["revestik:public-return-path"] =
            "/administracion";

        Assert.Equal("/es",
            GoogleLoginFlow.GetPublicReturnPath(login));
    }
}
