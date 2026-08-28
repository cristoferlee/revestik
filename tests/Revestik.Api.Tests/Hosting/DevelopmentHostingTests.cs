using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Revestik.Api.Tests.Hosting;

public sealed class DevelopmentHostingTests
    : IAsyncLifetime
{
    private readonly RevestikWebApplicationFactory factory =
        new("Development");

    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/customers")]
    public async Task ClientRoutes_AreNotServedAsHtmlByApi(
        string path)
    {
        using var response = await client.GetAsync(path);

        Assert.False(response.IsSuccessStatusCode);
        Assert.NotEqual(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ConfiguredClientOrigin_IsAllowedByCors()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/customers");
        request.Headers.Add(
            "Origin",
            "https://localhost:7081");
        request.Headers.Add(
            "Access-Control-Request-Method",
            "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "https://localhost:7081",
            response.Headers.GetValues(
                    "Access-Control-Allow-Origin")
                .Single());
    }

    [Fact]
    public async Task UnknownClientOrigin_IsNotAllowedByCors()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/customers");
        request.Headers.Add(
            "Origin",
            "https://untrusted.example");
        request.Headers.Add(
            "Access-Control-Request-Method",
            "GET");

        using var response = await client.SendAsync(request);

        Assert.False(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));
    }
}
