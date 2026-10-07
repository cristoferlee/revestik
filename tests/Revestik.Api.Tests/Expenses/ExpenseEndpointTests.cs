using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Common;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class ExpenseEndpointTests : IAsyncLifetime
{
    private const string TestUserId = "expense-endpoint-user";

    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new RevestikWebApplicationFactory("Development");

        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            TestUserId);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateAndListExpense_CompletesFlow()
    {
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/expenses",
            new ExpenseCreateRequest
            {
                Name = "Pago de planilla",
                Description = "Segunda quincena de septiembre.",
                TotalAmount = 850000m,
                ExpenseDate = new DateOnly(2026, 9, 30)
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(created);
        Assert.Equal("Pago de planilla", created.Name);

        using var listResponse = await client.GetAsync(
            "/api/expenses?Search=planilla&Page=1&PageSize=20");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var page = await listResponse.Content
            .ReadFromJsonAsync<PaginatedResponse<ExpenseListItemResponse>>();

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(created.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task CreateExpense_WithBlankDescription_ReturnsBadRequest()
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/expenses",
            new ExpenseCreateRequest
            {
                Name = "Gasto sin detalle",
                Description = " ",
                TotalAmount = 20000m,
                ExpenseDate = new DateOnly(2026, 10, 1)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_WithoutCsrf_ReturnsBadRequest()
    {
        using var response = await client.PostAsJsonAsync(
            "/api/expenses",
            new ExpenseCreateRequest
            {
                Name = "Gasto",
                Description = "Descripción obligatoria",
                TotalAmount = 1000m,
                ExpenseDate = new DateOnly(2026, 10, 1)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Expenses_WithSalesRole_ReturnForbidden()
    {
        using var unauthorizedClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        unauthorizedClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            TestUserId);
        unauthorizedClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName,
            "Sales");

        using var response = await unauthorizedClient.GetAsync(
            "/api/expenses?Page=1&PageSize=20");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Summary_WithInvalidDateRange_ReturnsBadRequest()
    {
        using var response = await client.GetAsync(
            "/api/expenses/summary?DateFrom=2026-10-31&DateTo=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        object? payload = null)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(method, path);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);
        return payload.RequestToken;
    }
}
