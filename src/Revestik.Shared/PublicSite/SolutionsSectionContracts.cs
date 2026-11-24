namespace Revestik.Shared.PublicSite;

// Bounded editorial copy for the seven known Solutions page sections.
// These contracts are intentionally separate from HomeSectionCopy and its media/products data.
public sealed class SolutionsSectionCopy
{
    public string Eyebrow { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public SolutionsSectionCopy Clone() => new()
    {
        Eyebrow = Eyebrow,
        Title = Title,
        Description = Description
    };
}

public sealed class SolutionsSectionDto
{
    public string Key { get; set; } = "";
    public string Version { get; set; } = "";
    public bool HasImage { get; set; }
    public SolutionsSectionCopy Es { get; set; } = new();
    public SolutionsSectionCopy? En { get; set; }
    public SolutionsSectionCopy? De { get; set; }

    public SolutionsSectionDto Clone() => new()
    {
        Key = Key,
        Version = Version,
        HasImage = HasImage,
        Es = Es.Clone(),
        En = En?.Clone(),
        De = De?.Clone()
    };

    public string Value(string language, string field)
    {
        var translated = language switch { "en" => En, "de" => De, _ => Es };
        var spanish = field switch
        {
            "eyebrow" => Es.Eyebrow,
            "title" => Es.Title,
            "description" => Es.Description,
            _ => ""
        };
        var candidate = field switch
        {
            "eyebrow" => translated?.Eyebrow,
            "title" => translated?.Title,
            "description" => translated?.Description,
            _ => null
        };
        return string.IsNullOrWhiteSpace(candidate) ? spanish : candidate;
    }
}

public sealed class SolutionsSectionWrite
{
    public string Version { get; set; } = "";
    public string? ImageToken { get; set; }
    public bool RemoveImage { get; set; }
    public SolutionsSectionCopy Es { get; set; } = new();
    public SolutionsSectionCopy? En { get; set; }
    public SolutionsSectionCopy? De { get; set; }
}
