using System.Net;
using System.Net.Http.Json;

namespace Revestik.Public.Services;

// Anonymous/published-only transport. Never forwards administrator cookies or internal project records.
public sealed class PublicProjectsReader(HttpClient http)
{
    public async Task<ProjectPageDto> List(string language, int page = 1, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<ProjectPageDto>(
            $"api/public/projects?language={Uri.EscapeDataString(language)}&page={Math.Clamp(page, 1, 10000)}", ct) ?? new();

    public async Task<List<ProjectPreviewDto>> Featured(string language, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProjectPreviewDto>>(
            $"api/public/projects/featured?language={Uri.EscapeDataString(language)}", ct) ?? [];

    public async Task<ProjectDetailDto?> Detail(string slug, string language, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"api/public/projects/{Uri.EscapeDataString(slug)}?language={Uri.EscapeDataString(language)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDetailDto>(cancellationToken: ct);
    }
}

// Matches the existing API's PublicProjectReader records; no new API contracts or tables.
public sealed class ProjectPageDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int Total { get; set; }
    public List<ProjectPreviewDto> Items { get; set; } = [];
}

public sealed class ProjectPreviewDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string? Location { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CoverImageId { get; set; }
    public string CoverAlt { get; set; } = "";
    public List<string> SpaceCodes { get; set; } = [];
}

public sealed class ProjectDetailDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string? Location { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SeoTitle { get; set; } = "";
    public string SeoDescription { get; set; } = "";
    public List<string> SpaceCodes { get; set; } = [];
    public List<ProjectPhotoDto> Photos { get; set; } = [];
}

public sealed class ProjectPhotoDto
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public int DisplayOrder { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Alt { get; set; } = "";
    public List<ProjectHotspotDto> Tags { get; set; } = [];
}

public sealed class ProjectHotspotDto
{
    public int Id { get; set; }
    public decimal AnchorX { get; set; }
    public decimal AnchorY { get; set; }
    public decimal LabelX { get; set; }
    public decimal LabelY { get; set; }
    public int DisplayOrder { get; set; }
    public string Label { get; set; } = "";
    public string ProductName { get; set; } = "";
    public bool ProductAvailable { get; set; }
    public string? ProductSlug { get; set; }
}
