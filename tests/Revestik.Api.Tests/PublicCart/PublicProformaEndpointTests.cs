using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicProformaEndpointTests
{
    private static HttpClient Client(RevestikWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task Anonymous_InvalidSelection_FailsWithoutCreatingPdf()
    {
        await using var factory = new RevestikWebApplicationFactory("Development");
        using var client = Client(factory);
        using var empty = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("es", []));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var duplicate = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("es", [new(1, 1m), new(1, 1m)]));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var invalid = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("es", [new(1, 0.0001m)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task CrossSiteOrigin_IsDeniedBeforePdfWork()
    {
        await using var factory = new RevestikWebApplicationFactory("Development");
        using var client = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/public/cart/proforma")
        {
            Content = JsonContent.Create(new PublicProformaRequest("es", [new(1, 1m)]))
        };
        request.Headers.Add("Origin", "https://untrusted.example");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
