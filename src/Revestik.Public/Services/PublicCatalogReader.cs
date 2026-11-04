using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.PublicSite;

namespace Revestik.Public.Services;

// Public-only client. No cookies, admin credentials, or inventory references.
public sealed class PublicCatalogReader(HttpClient http)
{
    public async Task<List<HomeSectionDto>> HomeSections(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<HomeSectionDto>>("api/public/site/home-sections", ct) ?? [];

    public async Task<HttpResponseMessage> HomeSolutionsImage(CancellationToken ct = default) =>
        await http.GetAsync("api/public/site/home-sections/solutions/media", HttpCompletionOption.ResponseHeadersRead, ct);

    public async Task<List<HomeHeroSlideDto>> HomeHeroSlides(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<HomeHeroSlideDto>>("api/public/site/home-hero", ct) ?? [];

    public async Task<HttpResponseMessage> HomeHeroImage(int slot, CancellationToken ct = default) =>
        await http.GetAsync($"api/public/site/home-hero/media/{slot}", HttpCompletionOption.ResponseHeadersRead, ct);

    public async Task<List<CategoryDto>> Categories(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CategoryDto>>("api/public/catalog/categories", ct) ?? [];

    public async Task<CategoryDetailDto?> Category(string slug, CancellationToken ct = default) =>
        await Optional<CategoryDetailDto>($"api/public/catalog/categories/{Uri.EscapeDataString(slug)}", ct);

    public async Task<ProductPageDto> Products(int page, int? categoryId, int? subcategoryId, string? search, CancellationToken ct = default)
    {
        var uri = $"api/public/catalog/products?page={Math.Max(1, page)}&pageSize=12";
        if (categoryId is not null) uri += $"&categoryId={categoryId}";
        if (subcategoryId is not null) uri += $"&subcategoryId={subcategoryId}";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&search={Uri.EscapeDataString(search)}";
        return await http.GetFromJsonAsync<ProductPageDto>(uri, ct) ?? new();
    }

    public Task<ProductDto?> Product(string slug, CancellationToken ct = default) =>
        Optional<ProductDto>($"api/public/catalog/products/{Uri.EscapeDataString(slug)}", ct);

    public async Task<HttpResponseMessage> Image(int id, CancellationToken ct = default) =>
        await http.GetAsync($"api/public/catalog/images/{id}", HttpCompletionOption.ResponseHeadersRead, ct);

    private async Task<T?> Optional<T>(string uri, CancellationToken ct)
    {
        using var response = await http.GetAsync(uri, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
}

public sealed class CategoryDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string NameEs { get; set; } = "";
    public string? NameEn { get; set; }
    public string? NameDe { get; set; }
    public string? DescriptionEs { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public string? HeroTitleEs { get; set; }
    public string? HeroTitleEn { get; set; }
    public string? HeroTitleDe { get; set; }
    public string? HeroDescriptionEs { get; set; }
    public string? HeroDescriptionEn { get; set; }
    public string? HeroDescriptionDe { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Version { get; set; }
    public byte[]? RowVersion { get; set; }
    public string Name(string lang) => lang switch { "en" => NameEn ?? NameEs, "de" => NameDe ?? NameEs, _ => NameEs };
    public string? Description(string lang) => lang switch { "en" => DescriptionEn ?? DescriptionEs, "de" => DescriptionDe ?? DescriptionEs, _ => DescriptionEs };
    public string? HeroTitle(string lang) => lang switch { "en" => HeroTitleEn ?? HeroTitleEs, "de" => HeroTitleDe ?? HeroTitleEs, _ => HeroTitleEs };
    public string? HeroDescription(string lang) => lang switch { "en" => HeroDescriptionEn ?? HeroDescriptionEs, "de" => HeroDescriptionDe ?? HeroDescriptionEs, _ => HeroDescriptionEs };
}
public sealed class SubcategoryDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Slug { get; set; } = "";
    public string NameEs { get; set; } = "";
    public string? NameEn { get; set; }
    public string? NameDe { get; set; }
    public bool IsActive { get; set; }
    public string Name(string lang) => lang switch { "en" => NameEn ?? NameEs, "de" => NameDe ?? NameEs, _ => NameEs };
}
public sealed class CategoryDetailDto
{
    public CategoryDto Category { get; set; } = new();
    public List<SubcategoryDto> Subcategories { get; set; } = [];
    public List<ImageDto> Images { get; set; } = [];
}
public sealed class ProductPageDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ProductDto> Items { get; set; } = [];
}
public sealed class ProductDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string? Brand { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? SalesUnit { get; set; }
    public bool IsPublished { get; set; }
    public int? CoverImageId { get; set; }
    public List<TranslationDto> Translations { get; set; } = [];
    public List<ImageDto> Images { get; set; } = [];
    public TranslationDto? Text(string language) => Translations.FirstOrDefault(t => t.LanguageCode == language) ?? Translations.FirstOrDefault(t => t.LanguageCode == "es");
    public string? ImageAlt(ImageDto image, string lang) => lang switch { "en" => image.AltEn ?? image.AltEs, "de" => image.AltDe ?? image.AltEs, _ => image.AltEs };
}
public sealed class TranslationDto
{
    public string LanguageCode { get; set; } = "es";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
public sealed class ImageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string? ContentType { get; set; }
    public int DisplayOrder { get; set; }
    public string? AltEs { get; set; }
    public string? AltEn { get; set; }
    public string? AltDe { get; set; }
    public int? FallbackImageId { get; set; }
    public string? Version { get; set; }
}

public sealed class HomeHeroSlideDto
{
    public int Slot { get; set; }
    public string HeadingEs { get; set; } = "";
    public string? HeadingEn { get; set; }
    public string? HeadingDe { get; set; }
    public string DescriptionEs { get; set; } = "";
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public bool HasImage { get; set; }
    public string Version { get; set; } = "";
    public string Heading(string lang) => lang switch { "en" => HeadingEn ?? HeadingEs, "de" => HeadingDe ?? HeadingEs, _ => HeadingEs };
    public string Description(string lang) => lang switch { "en" => DescriptionEn ?? DescriptionEs, "de" => DescriptionDe ?? DescriptionEs, _ => DescriptionEs };
}
