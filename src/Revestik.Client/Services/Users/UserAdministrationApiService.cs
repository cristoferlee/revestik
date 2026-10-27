using System.Net.Http.Json;
using Revestik.Shared.Users;

namespace Revestik.Client.Services.Users;

public sealed class UserAdministrationApiService(HttpClient client)
{
    public async Task<IReadOnlyList<ManagedUserResponse>> ListAsync(CancellationToken ct = default) =>
        (await client.GetFromJsonAsync<ManagedUserListResponse>("api/settings/users", ct))?.Users
        ?? [];

    public async Task CreateAsync(CreateManagedUserRequest request, CancellationToken ct = default)
    {
        using var response = await client.PostAsJsonAsync("api/settings/users", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, UpdateManagedUserRequest request, CancellationToken ct = default)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/settings/users/{Uri.EscapeDataString(id)}", request, ct);
        response.EnsureSuccessStatusCode();
    }
}
