namespace Revestik.Shared.PublicSite;

// Pure transport contracts; no server entities, HTML, authorization or persistence logic.
public sealed class HomeSectionCopy
{
    public string Eyebrow { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HomeAdvantageCopy> Cards { get; set; } = [];
    public HomeSectionCopy Clone() => new() { Eyebrow = Eyebrow, Title = Title, Description = Description,
        Cards = Cards.Select(c => new HomeAdvantageCopy { Title = c.Title, Description = c.Description }).ToList() };
}
public sealed class HomeAdvantageCopy
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
public sealed class HomeSectionDto
{
    public string Key { get; set; } = "";
    public string Version { get; set; } = "";
    public HomeSectionCopy Es { get; set; } = new();
    public HomeSectionCopy? En { get; set; }
    public HomeSectionCopy? De { get; set; }
    public List<string> FeaturedSlugs { get; set; } = [];
    public bool HasImage { get; set; }
    public HomeSectionDto Clone() => new() { Key = Key, Version = Version, Es = Es.Clone(), En = En?.Clone(), De = De?.Clone(),
        FeaturedSlugs = [..FeaturedSlugs], HasImage = HasImage };
    public HomeSectionCopy Text(string lang) => lang switch { "en" => En ?? Es, "de" => De ?? Es, _ => Es };
}
public sealed class HomeSectionWrite
{
    public string Version { get; set; } = "";
    public HomeSectionCopy Es { get; set; } = new();
    public HomeSectionCopy? En { get; set; }
    public HomeSectionCopy? De { get; set; }
    public List<string> FeaturedSlugs { get; set; } = [];
    public string? ImageToken { get; set; }
    public bool RemoveImage { get; set; }
}
