using System.Net.Http.Json;

namespace Revestik.Public.Services;

// Only anonymous, published page content; never forwards admin identity to the API.
public sealed class FinishesHeroReader(HttpClient http)
{
    public async Task<List<HomeHeroSlideDto>> Slides(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<HomeHeroSlideDto>>("api/public/site/finishes-hero", ct) ?? [];

    public Task<HttpResponseMessage> Image(int slot, CancellationToken ct = default) =>
        http.GetAsync($"api/public/site/finishes-hero/media/{slot}", HttpCompletionOption.ResponseHeadersRead, ct);
}
