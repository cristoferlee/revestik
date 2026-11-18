using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Authentication;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicGoogleLoginBoundaryTests
{
    [Theory]
    [InlineData("Development", false, false)]
    [InlineData("Development", true, true)]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, false)]
    public void PublicRegistration_IsDisabledByDefaultAndAlwaysDisabledInProduction(
        string environmentName, bool flag, bool expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:PublicGoogleRegistrationEnabled"] = flag.ToString()
            }).Build();

        Assert.Equal(expected, GoogleLoginFlow.PublicRegistrationEnabled(
            config, new FakeEnvironment(environmentName)));
    }

    [Fact]
    public async Task DisabledPublicRoutes_DoNotStartOrCompleteGoogleLogin()
    {
        // Do not depend on developer User Secrets: explicitly test disabled registration.
        using var baseFactory = new RevestikWebApplicationFactory("Development");
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:PublicGoogleRegistrationEnabled"] = "false"
                })));
        using var client = factory.CreateClient();
        using var login = await client.GetAsync("/api/auth/public/login/google");
        using var callback = await client.GetAsync("/api/auth/public/external-callback");
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, callback.StatusCode);
    }

    [Fact]
    public void Intent_IsBoundToExternalAuthenticationProperties()
    {
        var properties = new AuthenticationProperties();
        GoogleLoginFlow.SetIntent(properties, GoogleLoginFlow.Public);
        var login = MakeLogin(properties);
        Assert.True(GoogleLoginFlow.HasIntent(login, GoogleLoginFlow.Public));
        Assert.False(GoogleLoginFlow.HasIntent(login, GoogleLoginFlow.Internal));
        Assert.False(GoogleLoginFlow.HasIntent(MakeLogin(new AuthenticationProperties()),
            GoogleLoginFlow.Public));
    }

    [Fact]
    public void PublicRole_IsNeverAnEmployeeRole()
    {
        Assert.DoesNotContain(RoleNames.PublicCustomer, RoleNames.All);
        Assert.True(GoogleLoginFlow.HasPublicRole([RoleNames.PublicCustomer]));
        Assert.False(GoogleLoginFlow.HasEmployeeRole([RoleNames.PublicCustomer]));
        Assert.False(GoogleLoginFlow.HasPublicRole([RoleNames.Administrator]));
        Assert.False(GoogleLoginFlow.HasPublicRole(
            [RoleNames.PublicCustomer, RoleNames.Administrator]));
        Assert.False(GoogleLoginFlow.HasEmployeeRole(
            [RoleNames.PublicCustomer, RoleNames.Administrator]));
        foreach (var internalRole in RoleNames.All)
            Assert.True(GoogleLoginFlow.HasEmployeeRole([internalRole]));
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("de", "de")]
    [InlineData("https://attacker.example", "es")]
    [InlineData("../administracion", "es")]
    public void PublicLanguage_IsRestrictedToSupportedLocales(string requested, string expected)
    {
        var properties = new AuthenticationProperties();
        GoogleLoginFlow.SetPublicLanguage(properties, requested);
        Assert.Equal(expected, GoogleLoginFlow.GetPublicLanguage(MakeLogin(properties)));
    }

    [Fact]
    public void PublicIdentitySession_ExpiresAfterFourHours_WithoutPersistenceOrSliding()
    {
        var now = new DateTimeOffset(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);
        var session = GoogleLoginFlow.PublicSessionProperties(now);
        Assert.False(session.IsPersistent);
        Assert.Equal(now.AddHours(4), session.ExpiresUtc);
        Assert.False(session.AllowRefresh == true);
    }

    private static ExternalLoginInfo MakeLogin(AuthenticationProperties properties) =>
        new(new ClaimsPrincipal(new ClaimsIdentity([], "Google")), "Google", "google-sub", "Google")
        {
            AuthenticationProperties = properties
        };

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Revestik.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
