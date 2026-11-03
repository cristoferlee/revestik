namespace Revestik.Public;

// Approved HOME-01A.3 editorial drafts. Verify actual claims and complete page content before indexing.
public sealed record SeoCopy(string Title, string Heading, string Description);

public static class PublicSeo
{
    private static readonly SeoCopy[][] Pages =
    [
        [
            new("Porcelanatos y acabados en Costa Rica | REVESTIK", "Porcelanatos y acabados para construcción y remodelación en Costa Rica", "Conocé porcelanatos, revestimientos y soluciones en acabados para construcción y remodelación en Costa Rica. Explorá proyectos y solicitá asesoría."),
            new("Porcelain Tiles and Finishes in Costa Rica | REVESTIK", "Porcelain tiles and finishes for construction and renovation in Costa Rica", "Explore porcelain tiles, wall coverings and selected finishes for construction and renovation projects in Costa Rica. Contact REVESTIK for guidance."),
            new("Feinsteinzeug und Oberflächen in Costa Rica | REVESTIK", "Feinsteinzeug und Oberflächen für Bau- und Renovierungsprojekte in Costa Rica", "Entdecken Sie Feinsteinzeug, Wandverkleidungen und ausgewählte Oberflächen für Bau- und Renovierungsprojekte in Costa Rica. Kontaktieren Sie REVESTIK.")
        ],
        [
            new("Proyectos y acabados en Costa Rica | REVESTIK", "Proyectos con porcelanatos y acabados en Costa Rica", "Explorá proyectos con soluciones en pisos y revestimientos. Próximamente publicaremos fotografías y detalles verificados de proyectos REVESTIK."),
            new("Projects and Architectural Finishes | REVESTIK Costa Rica", "Projects featuring architectural finishes in Costa Rica", "Explore project highlights and selected finishes in Costa Rica. Verified project photographs and details will be published soon."),
            new("Projekte und Oberflächen in Costa Rica | REVESTIK", "Projekte mit ausgewählten Oberflächen in Costa Rica", "Entdecken Sie Projekte und ausgewählte Boden- und Wandbeläge. Verifizierte Projektbilder und Details folgen.")
        ],
        [
            new("Porcelanatos y revestimientos en Costa Rica | REVESTIK", "Porcelanatos, revestimientos y acabados para tus espacios", "Conocé las categorías de porcelanatos y revestimientos que REVESTIK prepara para proyectos de construcción y remodelación en Costa Rica."),
            new("Porcelain Tiles and Surface Finishes | REVESTIK", "Porcelain tiles, wall coverings and finishes for your spaces", "Discover porcelain tile and surface finish categories for construction and renovation projects in Costa Rica."),
            new("Feinsteinzeug und Wandverkleidungen | REVESTIK", "Feinsteinzeug, Wandverkleidungen und Oberflächen", "Entdecken Sie Kategorien von Boden- und Wandbelägen für Bau- und Renovierungsprojekte in Costa Rica.")
        ],
        [
            new("Soluciones en acabados para construcción | REVESTIK", "Soluciones en materiales y acabados para proyectos en Costa Rica", "Conocé las soluciones y materiales que REVESTIK presenta para proyectos de construcción y remodelación en Costa Rica."),
            new("Construction and Renovation Finishes | REVESTIK", "Materials and finishing solutions for projects in Costa Rica", "Learn about finishing materials and solutions for construction and renovation projects in Costa Rica."),
            new("Materialien und Oberflächen für Bauprojekte | REVESTIK", "Materialien und Oberflächen für Projekte in Costa Rica", "Erfahren Sie mehr über ausgewählte Materialien und Oberflächen für Bau- und Renovierungsprojekte in Costa Rica.")
        ],
        [
            new("Sobre REVESTIK | Acabados para construcción en Costa Rica", "REVESTIK: soluciones y acabados para la construcción", "Conocé REVESTIK y nuestro enfoque en materiales y acabados para proyectos de construcción y remodelación en Costa Rica."),
            new("About REVESTIK | Construction Finishes in Costa Rica", "REVESTIK: construction and renovation finishes", "Learn about REVESTIK and its focus on finishing materials for construction and renovation projects in Costa Rica."),
            new("Über REVESTIK | Oberflächen in Costa Rica", "REVESTIK: Materialien und Oberflächen für Bauprojekte", "Lernen Sie REVESTIK und unser Angebot an Materialien und Oberflächen für Projekte in Costa Rica kennen.")
        ],
        [
            new("Contacto y asesoría en acabados | REVESTIK Costa Rica", "Asesoría en porcelanatos y acabados para tu proyecto", "Contactá a REVESTIK para consultar sobre porcelanatos, revestimientos y acabados para tu proyecto de construcción o remodelación."),
            new("Contact REVESTIK | Finishes in Costa Rica", "Ask about porcelain tiles and finishes for your project", "Get in touch with REVESTIK about porcelain tiles, wall coverings and finishes for a construction or renovation project in Costa Rica."),
            new("REVESTIK Kontakt | Oberflächen in Costa Rica", "Beratung zu Feinsteinzeug und Oberflächen für Ihr Projekt", "Kontaktieren Sie REVESTIK zu Feinsteinzeug und Oberflächen für Bau- und Renovierungsprojekte in Costa Rica.")
        ]
    ];

    // Project details share the same reviewed example catalogue as Home and the project index.
    // No project metadata may be invented independently of its public content.
    public static SeoCopy? GetProject(string language, string? slug)
    {
        var project = PublicProjectCatalog.Find(language, slug);
        return project is null ? null : new SeoCopy(project.SeoTitle, project.Title, project.SeoDescription);
    }

    public static SeoCopy Get(string language, int page)
    {
        var languageIndex = language switch { "en" => 1, "de" => 2, _ => 0 };
        return Pages[Math.Clamp(page, 0, Pages.Length - 1)][languageIndex];
    }
}
