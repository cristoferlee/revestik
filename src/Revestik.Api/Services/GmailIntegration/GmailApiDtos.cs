using System.Text.Json.Serialization;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

internal sealed class GmailProfileDto
{
    [JsonPropertyName("emailAddress")]
    public string EmailAddress { get; set; } = string.Empty;
}

internal sealed class GmailMessageListDto
{
    [JsonPropertyName("messages")]
    public List<GmailMessageReferenceDto> Messages { get; set; } = [];

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

internal sealed class GmailMessageReferenceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

internal sealed class GmailMessageDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public GmailMessagePartDto? Payload { get; set; }
}

internal sealed class GmailMessagePartDto
{
    [JsonPropertyName("partId")]
    public string? PartId { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("headers")]
    public List<GmailMessageHeaderDto> Headers { get; set; } = [];

    [JsonPropertyName("body")]
    public GmailMessagePartBodyDto? Body { get; set; }

    [JsonPropertyName("parts")]
    public List<GmailMessagePartDto> Parts { get; set; } = [];
}

internal sealed class GmailMessageHeaderDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

internal sealed class GmailMessagePartBodyDto
{
    [JsonPropertyName("attachmentId")]
    public string? AttachmentId { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }
}

internal sealed class GmailAttachmentDto
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }
}
