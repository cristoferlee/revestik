namespace Revestik.Public;

// Editorial examples only. Never read the internal customer/project database here.
// WEB-CONTENT-01 will later supply a reviewed, published-only read model.
public sealed record PublicProjectPreview(
    string Slug,
    string Category,
    string Title,
    string Description,
    string ImageLabel,
    string Overview,
    string FeaturesNote,
    string MaterialsNote,
    string[] GalleryLabels,
    string SeoTitle,
    string SeoDescription);

public static class PublicProjectCatalog
{
    private static readonly string[] KnownSlugs =
    [
        "residencial-ejemplo", "interior-ejemplo", "comercial-ejemplo"
    ];

    public static bool IsKnownSlug(string? slug) =>
        slug is not null && KnownSlugs.Contains(slug, StringComparer.Ordinal);

    public static PublicProjectPreview? Find(string language, string? slug) =>
        GetExamples(language).FirstOrDefault(item =>
            string.Equals(item.Slug, slug, StringComparison.Ordinal));

    public static IReadOnlyList<PublicProjectPreview> GetExamples(string language) => language switch
    {
        "en" =>
        [
            new("residencial-ejemplo", "Residential", "Residential project",
                "Example text. Project details will be published here.",
                "Placeholder for residential project photograph",
                "Illustrative residential project page. No actual property, location or completed work is represented here.",
                "Project scope and verified characteristics pending approval.",
                "Materials, dimensions and REVESTIK's actual involvement pending verification.",
                ["Residential project — main photograph pending approval", "Residential project — additional photograph pending approval", "Residential project — finish detail photograph pending approval"],
                "Residential project example | REVESTIK", "Illustrative residential project page. Photographs, finishes and verified information are pending approval."),
            new("interior-ejemplo", "Finishes", "Interior spaces",
                "Example text. Approved materials and images will be added.",
                "Placeholder for interior finish photograph",
                "Illustrative interior finishes page. Images and descriptions will be replaced with approved information.",
                "Space type, work performed and verified characteristics pending approval.",
                "Specific finishes, suppliers and participation pending verification.",
                ["Interior spaces — main photograph pending approval", "Interior spaces — additional photograph pending approval", "Interior spaces — finish detail photograph pending approval"],
                "Interior spaces example | REVESTIK", "Illustrative interior finishes page. Verified materials, project photographs and details will follow."),
            new("comercial-ejemplo", "Commercial", "Commercial spaces",
                "Example text. Only verified project information will be displayed.",
                "Placeholder for commercial project photograph",
                "Illustrative commercial spaces page. No customer, building or completed contract is being presented.",
                "Business use, project scope and characteristics pending approval.",
                "Approved products and the verified role of REVESTIK pending publication.",
                ["Commercial spaces — main photograph pending approval", "Commercial spaces — additional photograph pending approval", "Commercial spaces — finish detail photograph pending approval"],
                "Commercial spaces example | REVESTIK", "Illustrative commercial project page. Images and project data will be published only after review.")
        ],
        "de" =>
        [
            new("residencial-ejemplo", "Wohnen", "Wohnprojekt",
                "Beispieltext. Projektdetails werden später veröffentlicht.",
                "Platzhalter für das Foto eines Wohnprojekts",
                "Beispielseite für ein Wohnprojekt. Es werden keine realen Immobilien oder abgeschlossenen Arbeiten dargestellt.",
                "Projektumfang und bestätigte Merkmale stehen noch aus.",
                "Materialien, Maße und die tatsächliche Beteiligung von REVESTIK müssen noch geprüft werden.",
                ["Wohnprojekt — Hauptfoto noch nicht freigegeben", "Wohnprojekt — weiteres Foto noch nicht freigegeben", "Wohnprojekt — Detailfoto noch nicht freigegeben"],
                "Beispiel eines Wohnprojekts | REVESTIK", "Beispielseite für ein Wohnprojekt. Bilder, Materialien und bestätigte Angaben stehen noch aus."),
            new("interior-ejemplo", "Oberflächen", "Innenräume",
                "Beispieltext. Freigegebene Materialien und Bilder folgen.",
                "Platzhalter für ein Foto von Innenräumen",
                "Beispielseite für Innenräume. Bilder und Beschreibungen werden durch freigegebene Inhalte ersetzt.",
                "Raumtyp, Leistungsumfang und bestätigte Merkmale stehen noch aus.",
                "Oberflächen, Lieferanten und Beteiligung müssen noch geprüft werden.",
                ["Innenräume — Hauptfoto noch nicht freigegeben", "Innenräume — weiteres Foto noch nicht freigegeben", "Innenräume — Detailfoto noch nicht freigegeben"],
                "Beispiel für Innenräume | REVESTIK", "Beispielseite für Innenräume. Bestätigte Materialien und Projektfotos folgen."),
            new("comercial-ejemplo", "Gewerbe", "Gewerbeflächen",
                "Beispieltext. Nur bestätigte Projektdaten werden angezeigt.",
                "Platzhalter für das Foto eines Gewerbeprojekts",
                "Beispielseite für Gewerbeflächen. Es werden keine tatsächlichen Kunden oder Aufträge dargestellt.",
                "Nutzung, Projektumfang und Merkmale stehen noch aus.",
                "Freigegebene Produkte und die bestätigte Rolle von REVESTIK folgen.",
                ["Gewerbeflächen — Hauptfoto noch nicht freigegeben", "Gewerbeflächen — weiteres Foto noch nicht freigegeben", "Gewerbeflächen — Detailfoto noch nicht freigegeben"],
                "Beispiel für Gewerbeflächen | REVESTIK", "Beispielseite für Gewerbeflächen. Bilder und Angaben folgen nach Prüfung.")
        ],
        _ =>
        [
            new("residencial-ejemplo", "Residencial", "Proyecto residencial",
                "Texto de ejemplo. Aquí mostraremos los detalles del proyecto.",
                "Espacio para fotografía de proyecto residencial",
                "Ficha ilustrativa de un proyecto residencial. No representa una vivienda real ni una obra ejecutada.",
                "Alcance, características y detalles confirmados pendientes de aprobación.",
                "Materiales, dimensiones y participación real de REVESTIK pendientes de verificación.",
                ["Proyecto residencial — fotografía principal pendiente", "Proyecto residencial — fotografía adicional pendiente", "Proyecto residencial — detalle de acabado pendiente"],
                "Ejemplo de proyecto residencial | REVESTIK", "Ficha ilustrativa de proyecto residencial. Fotografías, acabados e información verificada pendientes de aprobación."),
            new("interior-ejemplo", "Acabados", "Espacios interiores",
                "Texto de ejemplo. Incorporaremos materiales y fotografías aprobadas.",
                "Espacio para fotografía de acabados interiores",
                "Ficha ilustrativa de espacios interiores. Sustituiremos imágenes y descripciones con contenido aprobado.",
                "Tipo de espacio, trabajos realizados y características pendientes de confirmación.",
                "Acabados específicos, proveedores y participación pendientes de verificación.",
                ["Espacios interiores — fotografía principal pendiente", "Espacios interiores — fotografía adicional pendiente", "Espacios interiores — detalle de acabado pendiente"],
                "Ejemplo de espacios interiores | REVESTIK", "Ficha ilustrativa de acabados interiores. Materiales verificados, fotografías y detalles pendientes de publicación."),
            new("comercial-ejemplo", "Comercial", "Espacios comerciales",
                "Texto de ejemplo. Publicaremos únicamente información verificada.",
                "Espacio para fotografía de proyecto comercial",
                "Ficha ilustrativa de espacios comerciales. No se presenta ningún cliente, inmueble ni contrato real.",
                "Uso comercial, alcance y características pendientes de confirmación.",
                "Productos aprobados y participación verificada de REVESTIK pendientes de publicación.",
                ["Espacios comerciales — fotografía principal pendiente", "Espacios comerciales — fotografía adicional pendiente", "Espacios comerciales — detalle de acabado pendiente"],
                "Ejemplo de espacios comerciales | REVESTIK", "Ficha ilustrativa de proyecto comercial. Imágenes y datos se publicarán únicamente después de revisión.")
        ]
    };
}
