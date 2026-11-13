namespace Revestik.Api.Models.PublicProjects;

// es is required by the API before publication; en/de are optional with es fallback.
public sealed class PublicProjectTranslation
{
    public int ProjectId { get; set; }
    public PublicProject Project { get; set; } = null!;
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
