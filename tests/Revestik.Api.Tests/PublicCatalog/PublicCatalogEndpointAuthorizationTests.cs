using System.Net;
using Revestik.Api.Tests.Hosting;
using Xunit;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogEndpointAuthorizationTests
{
    [Fact]
    public async Task AdministrativeCategoriesRequireAuthentication()
    {
        using var factory=new RevestikWebApplicationFactory("Development");
        using var client=factory.CreateClient();
        var response=await client.GetAsync("/api/admin/public-catalog/categories");
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }

    [Fact]
    public async Task AdministrativeCategoriesForbidNonAdministrator()
    {
        using var factory=new RevestikWebApplicationFactory("Development");
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName,"catalog-tester");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName,"Sales");
        var response=await client.GetAsync("/api/admin/public-catalog/categories");
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Fact]
    public async Task AdministrativeMutationWithoutCsrfIsRejected()
    {
        using var factory=new RevestikWebApplicationFactory("Development");
        using var client=factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName,"catalog-admin");
        using var content=new StringContent("""{"slug":"csrf-test-category","nameEs":"Categoría de prueba","displayOrder":0,"isActive":true}""",System.Text.Encoding.UTF8,"application/json");
        var response=await client.PostAsync("/api/admin/public-catalog/categories",content);
        var responseBody=await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode==HttpStatusCode.BadRequest,
            $"Expected CSRF rejection (400), received {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
    }

    [Fact]
    public async Task AnonymousPublicCategoriesCanBeRead()
    {
        using var factory=new RevestikWebApplicationFactory("Development");
        using var client=factory.CreateClient();
        var response=await client.GetAsync("/api/public/catalog/categories");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }
    [Fact]
    public async Task VisualEditorProductsRequireAuthentication()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/public-catalog/products?page=1&pageSize=24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VisualEditorProductsForbidNonAdministrator()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "catalog-tester");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");
        var response = await client.GetAsync("/api/admin/public-catalog/products?page=1&pageSize=24");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmedContentMutationRejectsRequestWithoutCsrf()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "catalog-admin");
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/admin/public-catalog/products/1/content", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}
