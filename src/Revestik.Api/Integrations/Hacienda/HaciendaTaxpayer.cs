using System.Text.Json.Serialization;

namespace Revestik.Api.Integrations.Hacienda;

public sealed class HaciendaTaxpayer
{
    [JsonPropertyName("nombre")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("tipoIdentificacion")]
    public string IdentificationTypeCode { get; init; } = string.Empty;

    [JsonPropertyName("regimen")]
    public HaciendaTaxRegime? TaxRegime { get; init; }

    [JsonPropertyName("situacion")]
    public HaciendaTaxStatus? TaxStatus { get; init; }
}

public sealed class HaciendaTaxRegime
{
    [JsonPropertyName("codigo")]
    public int Code { get; init; }

    [JsonPropertyName("descripcion")]
    public string Description { get; init; } = string.Empty;
}

public sealed class HaciendaTaxStatus
{
    [JsonPropertyName("moroso")]
    public string Delinquent { get; init; } = string.Empty;

    [JsonPropertyName("omiso")]
    public string NonFiler { get; init; } = string.Empty;

    [JsonPropertyName("estado")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("administracionTributaria")]
    public string TaxAdministration { get; init; } = string.Empty;
}