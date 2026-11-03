namespace Revestik.Public;

public sealed record Labels(string[] Nav, string Menu, string Navigation, string Language, string Inquiry,
    string Tagline, string Skip, string HomeTitle, string Description, string Preview,
    string Pending, string FooterExplore, string FooterContact, string FooterLegal,
    string LegalPending, string GetInTouch, string Email, string Phone, string Coverage, string Hours);

public static class Locale
{
    public static string FromUri(string uri)
    {
        var path = new Uri(uri).AbsolutePath.Trim('/').Split('/')[0].ToLowerInvariant();
        return path is "en" or "de" ? path : "es";
    }

    public static Labels Get(string language) => language switch
    {
        "en" => new(["Home", "Projects", "Products", "Solutions", "About", "Contact"], "Menu", "Main navigation", "Language", "Request advice", "Finishes and solutions for construction", "Skip to content", "Spaces with purpose. Details with character.", "Discover projects and architectural finishes for building and renovating in Costa Rica.", "Visual preview: approved projects and imagery will arrive in the next HOME blocks.", "This page's verified commercial content will be prepared in the next blocks.", "Explore", "Contact", "Legal information", "Privacy policy, cookies and terms are pending legal review", "Tell us about your project", "Email pending verification", "WhatsApp pending verification", "Coverage pending verification", "Hours pending verification"),
        "de" => new(["Startseite", "Projekte", "Produkte", "Lösungen", "Über uns", "Kontakt"], "Menü", "Hauptnavigation", "Sprache", "Beratung anfragen", "Oberflächen und Lösungen für Bauprojekte", "Zum Inhalt springen", "Räume mit Persönlichkeit. Details mit Charakter.", "Entdecken Sie Projekte und hochwertige Oberflächen für Bau- und Renovierungsvorhaben in Costa Rica.", "Designvorschau: echte Projekte und Bilder folgen in den nächsten Schritten.", "Verifizierte kommerzielle Inhalte folgen in den nächsten Schritten.", "Entdecken", "Kontakt", "Rechtliche Hinweise", "Datenschutz, Cookies und Nutzungsbedingungen werden rechtlich geprüft", "Erzählen Sie uns von Ihrem Projekt", "E-Mail wird verifiziert", "WhatsApp wird verifiziert", "Einzugsgebiet wird verifiziert", "Öffnungszeiten werden verifiziert"),
        _ => new(["Inicio", "Proyectos", "Nuestros productos", "Soluciones", "Nosotros", "Contacto"], "Menú", "Navegación principal", "Idioma", "Solicitar asesoría", "Soluciones y acabados para la construcción", "Saltar al contenido", "Espacios con propósito. Detalles con carácter.", "Conocé proyectos y acabados para construcción y remodelación en Costa Rica.", "Vista preliminar: las fotografías y proyectos reales llegarán en los próximos bloques HOME.", "El contenido comercial verificado de esta página se desarrollará en los próximos bloques.", "Explorar", "Contacto", "Información legal", "Privacidad, cookies y términos pendientes de revisión legal", "Contanos sobre tu proyecto", "Correo pendiente de verificar", "WhatsApp pendiente de verificar", "Cobertura pendiente de verificar", "Horario pendiente de verificar")
    };
}
