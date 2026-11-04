namespace Revestik.Shared.PublicSite;

public static class HomeSectionDefaults
{
    public static HomeSectionCopy Spanish(string key) => key switch
    {
        "solutions" => new() { Eyebrow = "SOLUCIONES REVESTIK", Title = "Materiales para cada espacio.",
            Description = "Explorá porcelanatos, revestimientos y acabados seleccionados para construcción y remodelación. Consultanos por opciones para tu proyecto." },
        "advantages" => new() { Eyebrow = "CON REVESTIK", Title = "Una forma más cercana de elegir acabados.",
            Description = "Así buscamos facilitar la elección de materiales y la planificación de tus espacios.",
            Cards =
            [
                new() { Title = "Atención personalizada", Description = "Contanos qué necesitás y explorá opciones para tu espacio." },
                new() { Title = "Acabados seleccionados", Description = "Conocé nuestras categorías sin navegar un catálogo interminable." },
                new() { Title = "Enfoque en proyectos", Description = "Encontrá ideas para espacios residenciales y comerciales." },
                new() { Title = "Comunicación clara", Description = "Empezá con una conversación sobre lo que buscás." }
            ] },
        "contact" => new() { Eyebrow = "CONVERSEMOS", Title = "¿Tenés un proyecto en mente?",
            Description = "Contanos qué estás buscando. Queremos conocer tu proyecto." },
        "products" => new() { Eyebrow = "NUESTROS PRODUCTOS", Title = "Materiales que inspiran.",
            Description = "Conocé algunos de nuestros productos seleccionados para construcción y remodelación." },
        _ => new()
    };
}
