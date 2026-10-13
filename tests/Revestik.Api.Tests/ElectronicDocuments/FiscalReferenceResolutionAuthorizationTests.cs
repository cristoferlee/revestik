using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalReferenceResolutionAuthorizationTests : IAsyncLifetime
{
    private readonly RevestikWebApplicationFactory factory = new("Development");
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    private const string History = "/api/purchases/received-documents/fiscal/references/99999/resolution-history";
    private const string Authorize = "/api/purchases/received-documents/fiscal/references/99999/authorize-date-exception";
    private const string Link = "/api/purchases/received-documents/fiscal/references/99999/link";
    private const string Approve = "/api/purchases/received-documents/fiscal/notes/99999/approve";

    private static HttpRequestMessage Authenticated(HttpMethod method, string path, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "fiscal-http-test-user");
        request.Headers.TryAddWithoutValidation(TestAuthenticationHandler.RoleHeaderName, role);
        if (method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(new FiscalDateExceptionRequest(
                99999, "Justificaci\u00f3n documental para probar autorizaci\u00f3n."));
        }
        return request;
    }

    [Theory]
    [InlineData(History, false)]
    [InlineData(Authorize, true)]
    [InlineData(Link, true)]
    [InlineData(Approve, true)]
    public async Task Anonymous_IsRejected(string path, bool post)
    {
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, path);
        if (post) request.Content = JsonContent.Create(new FiscalDateExceptionRequest(99999, "Justificaci\u00f3n documental para pruebas."));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Sales)]
    [InlineData(RoleNames.Warehouse)]
    public async Task UnauthorizedAccountingRole_CannotViewResolutionHistory(string role)
    {
        using var request = Authenticated(HttpMethod.Get, History, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Sales, Authorize)]
    [InlineData(RoleNames.Warehouse, Authorize)]
    [InlineData(RoleNames.Sales, Link)]
    [InlineData(RoleNames.Warehouse, Link)]
    [InlineData(RoleNames.Sales, Approve)]
    [InlineData(RoleNames.Warehouse, Approve)]
    public async Task UnauthorizedAccountingRole_CannotMutateFiscalReferences(string role, string path)
    {
        using var request = Authenticated(HttpMethod.Post, path, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    public async Task AccountingRole_CanReachHistory(string role)
    {
        using var request = Authenticated(HttpMethod.Get, History, role);
        using var response = await client.SendAsync(request);
        // The fixture has no reference 99999; reaching the service proves authorization succeeded.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator, Authorize)]
    [InlineData(RoleNames.Accountant, Authorize)]
    [InlineData(RoleNames.Administrator, Link)]
    [InlineData(RoleNames.Accountant, Link)]
    [InlineData(RoleNames.Administrator, Approve)]
    [InlineData(RoleNames.Accountant, Approve)]
    public async Task AccountingRole_WithoutCsrf_IsRejected(string role, string path)
    {
        using var request = Authenticated(HttpMethod.Post, path, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
