namespace Revestik.Public.Services;

// Shared fallback copy for SSR and the inline editor. Never saved until approved.
public static class FinishesHeroDefaults
{
    public static HomeHeroSlideDto ForSlot(int slot) => slot switch
    {
        1 => new()
        {
            Slot = 1,
            HeadingEs = "Porcelanatos, revestimientos y acabados para tus espacios",
            HeadingEn = "Porcelain tiles and finishes for your spaces",
            HeadingDe = "Feinsteinzeug und Oberflächen für Ihre Räume",
            DescriptionEs = "Explorá productos seleccionados para construcción y remodelación en Costa Rica.",
            DescriptionEn = "Explore selected products for construction and renovation projects in Costa Rica.",
            DescriptionDe = "Entdecken Sie ausgewählte Produkte für Bau- und Renovierungsprojekte in Costa Rica."
        },
        2 => new()
        {
            Slot = 2,
            HeadingEs = "Materiales para cada proyecto",
            HeadingEn = "Materials for every project",
            HeadingDe = "Materialien für jedes Projekt",
            DescriptionEs = "Conocé opciones para pisos, paredes y espacios que querés transformar.",
            DescriptionEn = "Discover surfaces for floors, walls and spaces you want to transform.",
            DescriptionDe = "Entdecken Sie Beläge für Böden, Wände und Räume, die Sie gestalten möchten."
        },
        3 => new()
        {
            Slot = 3,
            HeadingEs = "Encontrá el acabado ideal",
            HeadingEn = "Find the finish that fits",
            HeadingDe = "Finden Sie die passende Oberfläche",
            DescriptionEs = "Explorá nuestras categorías y consultanos por opciones para tu espacio.",
            DescriptionEn = "Browse our categories and ask us about suitable options for your space.",
            DescriptionDe = "Durchsuchen Sie unsere Kategorien und fragen Sie uns nach passenden Optionen."
        },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
