using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

// The public portfolio has anonymous GET routes, but the administration API must not.
[Collection("Isolated production hosting tests")]
public sealed class PublicProjectAdminSecurityTests(ProductionHttpHostFixture fixture)
    : IClassFixture<ProductionHttpHostFixture>
{
    [Theory]
    [InlineData("/api/admin/public-projects")]
    [InlineData("/api/admin/public-projects/1")]
    public async Task AnonymousUsersCannotReadAdministrativeProjectData(string path)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);
        Assert.False(response.IsSuccessStatusCode);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or
            HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUsersCannotCreatePublishedProjects()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.PostAsync("/api/admin/public-projects",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.False(response.IsSuccessStatusCode);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or
            HttpStatusCode.Redirect);
    }
}
