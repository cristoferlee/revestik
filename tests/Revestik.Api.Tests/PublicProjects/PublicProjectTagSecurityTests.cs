using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

[Collection("Isolated production hosting tests")]
public sealed class PublicProjectTagSecurityTests(ProductionHttpHostFixture fixture)
    : IClassFixture<ProductionHttpHostFixture>
{
    [Theory]
    [InlineData("/api/admin/public-projects/products?search=porcelanato")]
    [InlineData("/api/admin/public-projects/images/1/tags")]
    public async Task AnonymousUserCannotReadAdministrativeTagsOrProductPicker(string path)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUserCannotCreateTag()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.PostAsync("/api/admin/public-projects/images/1/tags",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
    }
}
