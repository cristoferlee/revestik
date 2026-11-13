namespace Revestik.Api.Models.PublicProjects;

// Version 1 fixed editorial vocabulary. Codes remain stable when editing the vocabulary
// becomes an approved feature. No inventory taxonomy is reused.
public static class PublicProjectClassification
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "residential", "commercial", "hospitality", "institutional", "renovation", "corporate"
    };

    private static readonly HashSet<string> Spaces = new(StringComparer.Ordinal)
    {
        "kitchen", "living-room", "bathroom", "pool", "showroom", "terrace", "facade",
        "bedroom", "dining-room", "office", "lobby", "outdoor", "common-area"
    };

    public static bool IsKnownType(string? code) => code is not null && Types.Contains(code);
    public static bool IsKnownSpace(string? code) => code is not null && Spaces.Contains(code);
}
