using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicCartEndpointAuthorizationTests
{
    private static HttpClient Client(RevestikWebApplicationFactory factory, string? role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "cart-boundary-user");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        return client;
    }

    [Fact]
    public async Task AnonymousCannotReadOrMutateCart()
    {
        await using var factory = new RevestikWebApplicationFactory("Development");
        using var client = Client(factory, null);
        using var read = await client.GetAsync("/api/public/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        using var write = await client.PutAsJsonAsync("/api/public/cart/items/1",
            new PublicCartQuantityWrite(1m, null));
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Administrator")]
    [InlineData("Warehouse")]
    [InlineData("")]
    public async Task InternalOrUnassignedRolesCannotReadCart(string role)
    {
        await using var factory = new RevestikWebApplicationFactory("Development");
        using var client = Client(factory, role);
        using var read = await client.GetAsync("/api/public/cart");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task PublicRoleCannotMutateWithoutCsrf()
    {
        await using var factory = new RevestikWebApplicationFactory("Development");
        using var client = Client(factory, "PublicCustomer");
        using var write = await client.PutAsJsonAsync("/api/public/cart/items/1",
            new PublicCartQuantityWrite(1m, null));
        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
        using var delete = await client.DeleteAsync("/api/public/cart/items/1?version=AAAAAAAAAAA=");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
    }
}
