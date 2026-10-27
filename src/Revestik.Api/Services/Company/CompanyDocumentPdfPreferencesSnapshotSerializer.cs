using System.Text.Json;

namespace Revestik.Api.Services.Company;

// Pure serializer. Null means a pre-CONFIG-04 document, not current preferences.
public static class CompanyDocumentPdfPreferencesSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(CompanyDocumentPdfPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Validate(preferences);
        return JsonSerializer.Serialize(preferences, Options);
    }

    public static CompanyDocumentPdfPreferences? Deserialize(string? json)
    {
        if (json is null)
            return null;

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("El snapshot de preferencias PDF está vacío.");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("footerMessage", out var footer) ||
                footer.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("quotationDefaultTerms", out var terms) ||
                terms.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("showWebsite", out var website) ||
                website.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !root.TryGetProperty("showQuotationSeller", out var seller) ||
                seller.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("El snapshot de preferencias PDF carece de campos válidos.");
            }

            var result = JsonSerializer.Deserialize<CompanyDocumentPdfPreferences>(json, Options)
                ?? throw new InvalidDataException("No se pudo recuperar el snapshot PDF.");
            Validate(result);
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("El snapshot de preferencias PDF contiene JSON inválido.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("El snapshot de preferencias PDF contiene valores inválidos.", ex);
        }
    }

    public static void Validate(CompanyDocumentPdfPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.FooterMessage is null || preferences.FooterMessage.Length > 500 ||
            preferences.QuotationDefaultTerms is null || preferences.QuotationDefaultTerms.Length > 1000)
        {
            throw new ArgumentException("Los textos de preferencias PDF superan los límites permitidos o son nulos.");
        }
    }
}
