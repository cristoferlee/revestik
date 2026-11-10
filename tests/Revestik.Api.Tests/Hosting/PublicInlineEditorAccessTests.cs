using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Revestik.Api.Tests.Hosting;

public sealed class PublicInlineEditorAccessTests(DevelopmentHttpHostFixture fixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("sales-user", "Sales", false)]
    [InlineData("admin-user", "Administrator", true)]
    public async Task EditorAssets_AppearOnlyForOptedInAdministrator(
        string? user, string? role, bool expectedEditor)
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/es?edit=1");
        if (user is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("REVESTIK", html);
        Assert.Equal(expectedEditor, html.Contains("/css/public-inline-editor.css", StringComparison.Ordinal));
        Assert.Equal(expectedEditor, html.Contains("/_framework/blazor.web.js", StringComparison.Ordinal));

        if (expectedEditor)
        {
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        }
    }

    [Theory]
    [InlineData("/es/contacto?edit=1")]
    [InlineData("/en/about?edit=1")]
    public async Task NavigationKeepsEditorShellOnOtherPublicPages(string route)
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "admin-user");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, "Administrator");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/css/public-inline-editor.css", html);
        Assert.Contains("/_framework/blazor.web.js", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task NonAdministratorCannotEnableEditorOnOtherPublicPage()
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/es/contacto?edit=1");
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "sales-user");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/css/public-inline-editor.css", html);
        Assert.DoesNotContain("/_framework/blazor.web.js", html);
    }

    [Fact]
    public async Task EnablingEditor_WritesSecureServerSessionCookie()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/es?edit=1");
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "admin-user");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, "Administrator");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values));
        var preference = Assert.Single(values!, x =>
            x.StartsWith("Revestik.Public.EditMode=1;", StringComparison.Ordinal));
        Assert.Contains("secure", preference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", preference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", preference, StringComparison.OrdinalIgnoreCase);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-enhance-nav=\"false\"", html);
    }

    [Fact]
    public async Task ExitingEditor_ClearsServerPreferenceEvenIfOldCookieIsSent()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/es?edit=0");
        request.Headers.Add("Cookie", "Revestik.Public.EditMode=1");
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "admin-user");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, "Administrator");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values));
        Assert.Contains(values!, x => x.StartsWith("Revestik.Public.EditMode=;", StringComparison.Ordinal));
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/css/public-inline-editor.css", html);
        Assert.DoesNotContain("/_framework/blazor.web.js", html);
    }

    // 01B reuses the existing admin endpoint; no new editing API is exposed publicly.
    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData("sales-user", "Sales", HttpStatusCode.Forbidden)]
    public async Task HomeSectionAdminEndpoint_RejectsNonAdministrators(
        string? user, string? role, HttpStatusCode expectedStatus)
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/public-site/home-sections");
        if (user is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task OrdinaryHome_KeepsSectionsInSsrWithoutInlineEditor()
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var response = await client.GetAsync("/es");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("home-solutions", html);
        Assert.Contains("home-advantages", html);
        Assert.Contains("home-contact-cta", html);
        Assert.DoesNotContain("/css/public-inline-editor.css", html);
    }

    [Fact]
    public async Task OrdinaryPublicHome_DoesNotLoadEditor()
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        using var response = await client.GetAsync("/es");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/css/public-inline-editor.css", html);
        Assert.DoesNotContain("/_framework/blazor.web.js", html);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("sales-user", "Sales", false)]
    [InlineData("admin-user", "Administrator", true)]
    public async Task PersistentEditorCookie_RequiresAdministratorEveryTime(
        string? user, string? role, bool expectEditor)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/en/finishes");
        request.Headers.Add("Cookie", "Revestik.Public.EditMode=1");
        if (user is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectEditor, html.Contains("/css/public-inline-editor.css", StringComparison.Ordinal));
        Assert.Equal(expectEditor, html.Contains("/_framework/blazor.web.js", StringComparison.Ordinal));
        if (expectEditor)
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task EditingPreference_DoesNotLeakIntoAnonymousPublicHtml()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/es/acabados");
        request.Headers.Add("Cookie", "Revestik.Public.EditMode=1");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/css/public-inline-editor.css", html);
        Assert.DoesNotContain("/_framework/blazor.web.js", html);
    }

    [Theory]
    [InlineData("/api/admin/public-site/finishes-hero", null, null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/admin/public-site/finishes-hero", "staff", "Sales", HttpStatusCode.Forbidden)]
    [InlineData("/api/admin/public-catalog/products", null, null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/admin/public-catalog/products", "staff", "Sales", HttpStatusCode.Forbidden)]
    public async Task AcabadosEditorApis_RejectUnauthorisedReads(
        string path, string? user, string? role, HttpStatusCode expected)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }
}
