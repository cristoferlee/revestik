namespace Revestik.Shared.PublicSite;

/// <summary>
/// Exact editorial copy previously rendered by Solutions.razor, including all three
/// languages. These values are used until a section receives its first SQL write.
/// </summary>
public static class SolutionsSectionDefaults
{
    public static readonly string[] Keys =
    [
        "hero", "approach", "applications", "interior-flooring",
        "exterior-flooring", "walls-facades", "cta"
    ];

    public static bool IsKnown(string key) => Keys.Contains(key, StringComparer.Ordinal);

    public static bool AllowsField(string key, string field) =>
        IsKnown(key) && ((key == "hero" && field == "image") || field is "title" or "description" ||
                         (field == "eyebrow" && key is ("hero" or "approach" or "applications" or "cta")));

    public static bool HasTranslation(SolutionsSectionCopy? copy) => copy is not null &&
        (!string.IsNullOrWhiteSpace(copy.Eyebrow) || !string.IsNullOrWhiteSpace(copy.Title) ||
         !string.IsNullOrWhiteSpace(copy.Description));

    public static bool ValidCopy(string key, SolutionsSectionCopy? copy, bool required)
    {
        if (!IsKnown(key)) return false;
        if (copy is null) return !required;
        if (copy.Eyebrow is null || copy.Title is null || copy.Description is null ||
            copy.Eyebrow.Length > 90 || copy.Title.Length > 180 || copy.Description.Length > 1000)
            return false;
        if (key is "interior-flooring" or "exterior-flooring" or "walls-facades")
        {
            if (copy.Eyebrow.Length != 0 || copy.Description.Length > 500) return false;
        }
        if (key == "applications" && !string.IsNullOrEmpty(copy.Description)) return false;
        // ES is mandatory. EN and DE may be partial; each missing field falls back to ES.
        if (required && (string.IsNullOrWhiteSpace(copy.Title) ||
                         (key != "applications" && string.IsNullOrWhiteSpace(copy.Description))))
            return false;
        return true;
    }

    public static SolutionsSectionDto Create(string key)
    {
        if (!IsKnown(key)) throw new ArgumentOutOfRangeException(nameof(key));
        return new SolutionsSectionDto
        {
            Key = key,
            Es = Copy(key, "es"),
            En = Copy(key, "en"),
            De = Copy(key, "de")
        };
    }

    public static SolutionsSectionCopy Copy(string key, string language) => (key, language) switch
    {
        ("hero", "en") => new()
        {
            Eyebrow = "REVESTIK · SOLUTIONS",
            Title = "Materials and finishing solutions for projects in Costa Rica",
            Description = "Explore materials and finishes with the space, its purpose and the character of each project in mind."
        },
        ("hero", "de") => new()
        {
            Eyebrow = "REVESTIK · LÖSUNGEN",
            Title = "Materialien und Oberflächen für Projekte in Costa Rica",
            Description = "Entdecken Sie Materialien und Oberflächen passend zum Raum, seiner Nutzung und dem Charakter jedes Projekts."
        },
        ("hero", _) => new()
        {
            Eyebrow = "REVESTIK · SOLUCIONES",
            Title = "Soluciones en materiales y acabados para proyectos en Costa Rica",
            Description = "Explorá materiales y acabados pensando en el espacio, el uso y la identidad de cada proyecto."
        },
        ("approach", "en") => new()
        {
            Eyebrow = "OUR APPROACH",
            Title = "Different spaces call for different finishes.",
            Description = "An interior floor, a terrace and a wall each have different needs. Our public catalog brings together finishing options without turning the selection process into an endless product list."
        },
        ("approach", "de") => new()
        {
            Eyebrow = "UNSER ANSATZ",
            Title = "Jeder Raum stellt andere Anforderungen.",
            Description = "Innenböden, Terrassen und Wände stellen unterschiedliche Anforderungen. Unser öffentlicher Katalog bietet eine überschaubare Auswahl an Oberflächen für verschiedene Anwendungen."
        },
        ("approach", _) => new()
        {
            Eyebrow = "EL ENFOQUE",
            Title = "Cada espacio pide una solución diferente.",
            Description = "Un piso interior, una terraza o un muro tienen necesidades distintas. Nuestro catálogo público reúne opciones para explorar acabados sin convertir la elección en una lista interminable de productos."
        },
        ("applications", "en") => new() { Eyebrow = "APPLICATIONS", Title = "Explore finish options" },
        ("applications", "de") => new() { Eyebrow = "ANWENDUNGEN", Title = "Oberflächen entdecken" },
        ("applications", _) => new() { Eyebrow = "APLICACIONES", Title = "Acabados para explorar" },
        ("interior-flooring", "en") => new()
        {
            Title = "Interior flooring",
            Description = "Surfaces for residential and commercial interiors. Compare formats, looks and characteristics for each space."
        },
        ("interior-flooring", "de") => new()
        {
            Title = "Bodenbeläge für Innenräume",
            Description = "Oberflächen für Wohn- und Geschäftsräume. Vergleichen Sie Formate, Optik und Eigenschaften für den jeweiligen Raum."
        },
        ("interior-flooring", _) => new()
        {
            Title = "Pisos interiores",
            Description = "Superficies para espacios residenciales y comerciales. Compará formatos, apariencias y características según el ambiente."
        },
        ("exterior-flooring", "en") => new()
        {
            Title = "Outdoor flooring",
            Description = "Options for weather-exposed areas, where texture, maintenance and installation conditions deserve attention."
        },
        ("exterior-flooring", "de") => new()
        {
            Title = "Bodenbeläge für Außenbereiche",
            Description = "Optionen für wetterexponierte Bereiche, bei denen Oberfläche, Pflege und Verlegebedingungen wichtig sind."
        },
        ("exterior-flooring", _) => new()
        {
            Title = "Pisos exteriores",
            Description = "Opciones para áreas expuestas al clima, donde conviene revisar textura, mantenimiento y condiciones de instalación."
        },
        ("walls-facades", "en") => new()
        {
            Title = "Walls and facades",
            Description = "Wall coverings and materials that bring character to walls, wet areas and vertical surfaces."
        },
        ("walls-facades", "de") => new()
        {
            Title = "Wände und Fassaden",
            Description = "Verkleidungen und Materialien, die Wänden, Feuchtbereichen und vertikalen Flächen Charakter verleihen."
        },
        ("walls-facades", _) => new()
        {
            Title = "Paredes y fachadas",
            Description = "Revestimientos y materiales que aportan carácter a muros, zonas húmedas y superficies verticales."
        },
        ("cta", "en") => new()
        {
            Eyebrow = "NEXT STEP",
            Title = "Find finishes for your next project.",
            Description = "Browse published products or tell us about the space you're planning."
        },
        ("cta", "de") => new()
        {
            Eyebrow = "NÄCHSTER SCHRITT",
            Title = "Finden Sie Oberflächen für Ihr nächstes Projekt.",
            Description = "Entdecken Sie veröffentlichte Produkte oder erzählen Sie uns von Ihrem Projekt."
        },
        ("cta", _) => new()
        {
            Eyebrow = "SIGUIENTE PASO",
            Title = "Encontrá el acabado que va con tu proyecto.",
            Description = "Conocé los productos publicados o contanos qué espacio estás planificando."
        },
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}
