namespace Revestik.Public.Services;

// Public editorial labels for the existing, fixed classification codes.
public static class ProjectTypeLabel
{
    public static string Get(string code, string language) => (code, language) switch
    {
        ("residential", "en") => "Residential", ("residential", "de") => "Wohnen", ("residential", _) => "Residencial",
        ("commercial", "en") => "Commercial", ("commercial", "de") => "Gewerbe", ("commercial", _) => "Comercial",
        ("hospitality", "en") => "Hospitality", ("hospitality", "de") => "Hotellerie", ("hospitality", _) => "Hotelero",
        ("institutional", "en") => "Institutional", ("institutional", "de") => "Institutionell", ("institutional", _) => "Institucional",
        ("renovation", "en") => "Renovation", ("renovation", "de") => "Renovierung", ("renovation", _) => "Remodelación",
        ("corporate", "en") => "Corporate", ("corporate", "de") => "Unternehmen", ("corporate", _) => "Corporativo",
        _ => code
    };
}
