using System.Net.Http.Json;
using Revestik.Shared.Users;

namespace Revestik.Client.Services.Users;

public sealed class PublicCustomerAdministrationApiService(
    HttpClient client)
{
    public async Task<PublicCustomerListResponse> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var uri =
            $"api/settings/public-customers?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
            uri += "&search=" + Uri.EscapeDataString(search.Trim());

        return await client.GetFromJsonAsync<PublicCustomerListResponse>(
            uri, ct)
            ?? throw new HttpRequestException(
                "Empty public customer response.");
    }

    public async Task UpdateStatusAsync(
        string id,
        UpdatePublicCustomerStatusRequest request,
        CancellationToken ct = default)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/settings/public-customers/{Uri.EscapeDataString(id)}/status",
            request, ct);

        response.EnsureSuccessStatusCode();
    }
}
