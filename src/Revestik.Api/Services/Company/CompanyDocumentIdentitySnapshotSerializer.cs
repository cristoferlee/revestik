using System.Text.Json;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Services.Company;

// Pure serializer: no I/O, DB, or HTTP dependencies.
public static class CompanyDocumentIdentitySnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(CompanyDocumentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return JsonSerializer.Serialize(identity, Options);
    }

    public static CompanyDocumentIdentity? Deserialize(string? json)
    {
        if (json is null)
            return null; // Legacy document: no historical snapshot.

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("El snapshot de identidad empresarial está vacío.");

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("El snapshot de identidad empresarial no es un objeto JSON.");

            // An empty or unrelated object must never silently become a default identity.
            if (!document.RootElement.TryGetProperty("legalName", out var legalName) ||
                legalName.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(legalName.GetString()) ||
                !document.RootElement.TryGetProperty("identificationNumber", out var identification) ||
                identification.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(identification.GetString()))
            {
                throw new InvalidDataException("El snapshot de identidad empresarial carece de campos obligatorios.");
            }

            var result = JsonSerializer.Deserialize<CompanyDocumentIdentity>(json, Options);
            return result ?? throw new InvalidDataException("El snapshot empresarial no pudo recuperarse.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("El snapshot de identidad empresarial contiene JSON inválido.", ex);
        }
    }
}
