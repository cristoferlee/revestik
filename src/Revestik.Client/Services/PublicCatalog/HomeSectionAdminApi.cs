using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.PublicSite;

namespace Revestik.Client.Services.PublicCatalog;

// Reuses the application's authorized, antiforgery-enabled HttpClient.
public sealed class HomeSectionAdminApi(HttpClient http)
{
    private const string Root = "api/admin/public-site/home-sections";
    public async Task<List<HomeSectionDto>> Sections() =>
        await http.GetFromJsonAsync<List<HomeSectionDto>>(Root) ?? [];
    public string SolutionsImageUri(string version) => new Uri(http.BaseAddress ??
        throw new InvalidOperationException("Falta configurar la API."), $"{Root}/solutions/media?v={Uri.EscapeDataString(version)}").ToString();

    public async Task<HomeSectionDto> Save(string key, HomeSectionWrite content)
    {
        if (key is not ("solutions" or "advantages" or "contact" or "products"))
            throw new InvalidOperationException("Sección no válida.");
        using var response = await http.PutAsJsonAsync($"{Root}/{key}", content);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("La sección cambió desde otra sesión. Recargá la página antes de editar.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException("El servidor rechazó los datos. Revisá los campos y los productos publicados.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Tu sesión no tiene autorización para guardar esta sección.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HomeSectionDto>()
            ?? throw new InvalidOperationException("No se recibió la sección actualizada.");
    }
}
