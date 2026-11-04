using System.Net;
using System.Net.Http.Json;

namespace Revestik.Client.Services.PublicCatalog;

// Existing registered HttpClient carries the same Identity cookie and CSRF handler as all admin APIs.
public sealed class HomeHeroAdminApi(HttpClient http)
{
    private const string Root = "api/admin/public-site/home-hero";
    public async Task<List<HomeHeroSlideEditorModel>> Slides() =>
        await http.GetFromJsonAsync<List<HomeHeroSlideEditorModel>>(Root) ?? [];

    public string ImageUri(int slot) => new Uri(http.BaseAddress ?? throw new InvalidOperationException("API address missing"), $"{Root}/media/{slot}").ToString();

    public async Task<HomeHeroSlideEditorModel> Save(int slot, HomeHeroSlideEditorModel model, string? imageToken, bool removeImage)
    {
        using var response = await http.PutAsJsonAsync($"{Root}/{slot}", new
        {
            model.Version, model.HeadingEs, model.HeadingEn, model.HeadingDe,
            model.DescriptionEs, model.DescriptionEn, model.DescriptionDe,
            ImageToken = imageToken, RemoveImage = removeImage
        });
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("La portada cambió en otra sesión. Volvé a cargar el editor.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException("Revisá el título, la descripción en español y la imagen seleccionada.");
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("La sesión no tiene autorización para guardar esta portada.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HomeHeroSlideEditorModel>()
            ?? throw new InvalidOperationException("El servidor no devolvió la portada guardada.");
    }
}

public sealed class HomeHeroSlideEditorModel
{
    public int Slot { get; set; }
    public string Version { get; set; } = "";
    public string HeadingEs { get; set; } = "";
    public string? HeadingEn { get; set; }
    public string? HeadingDe { get; set; }
    public string DescriptionEs { get; set; } = "";
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public bool HasImage { get; set; }
    public HomeHeroSlideEditorModel Clone() => (HomeHeroSlideEditorModel)MemberwiseClone();
}
