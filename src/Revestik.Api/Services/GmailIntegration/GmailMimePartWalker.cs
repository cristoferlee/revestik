using System.Text;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed record GmailXmlAttachmentCandidate(
    string FileName,
    string Identity,
    string? AttachmentId,
    string? InlineData,
    int DeclaredSize);

internal sealed record GmailTextBodyCandidate(
    string MimeType,
    string Identity,
    string? AttachmentId,
    string? InlineData,
    int DeclaredSize);

internal static class GmailMimePartWalker
{
    public static IReadOnlyList<GmailXmlAttachmentCandidate> FindXmlAttachments(
        GmailMessagePartDto? root)
    {
        if (root is null)
            return [];

        var results = new List<GmailXmlAttachmentCandidate>();
        VisitXml(root, results);
        return results;
    }

    public static IReadOnlyList<GmailTextBodyCandidate> FindTextBodies(
        GmailMessagePartDto? root)
    {
        if (root is null)
            return [];

        var results = new List<GmailTextBodyCandidate>();
        VisitText(root, results);
        return results;
    }

    public static string? GetHeader(
        GmailMessagePartDto? root,
        string headerName)
    {
        if (root is null || string.IsNullOrWhiteSpace(headerName))
            return null;

        return root.Headers
            .FirstOrDefault(x => string.Equals(
                x.Name,
                headerName,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static byte[] DecodeBase64Url(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return [];

        var normalized = data
            .Replace('-', '+')
            .Replace('_', '/');

        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };

        return Convert.FromBase64String(normalized);
    }

    public static string DecodeBase64UrlUtf8(string data) =>
        Encoding.UTF8.GetString(DecodeBase64Url(data));

    private static void VisitXml(
        GmailMessagePartDto part,
        ICollection<GmailXmlAttachmentCandidate> results)
    {
        if (!string.IsNullOrWhiteSpace(part.FileName) &&
            string.Equals(
                Path.GetExtension(part.FileName),
                ".xml",
                StringComparison.OrdinalIgnoreCase) &&
            part.Body is not null)
        {
            var identity = !string.IsNullOrWhiteSpace(part.Body.AttachmentId)
                ? part.Body.AttachmentId
                : $"part:{part.PartId ?? Guid.NewGuid().ToString("N")}";

            results.Add(
                new GmailXmlAttachmentCandidate(
                    Path.GetFileName(part.FileName),
                    identity,
                    part.Body.AttachmentId,
                    part.Body.Data,
                    part.Body.Size));
        }

        foreach (var child in part.Parts)
            VisitXml(child, results);
    }

    private static void VisitText(
        GmailMessagePartDto part,
        ICollection<GmailTextBodyCandidate> results)
    {
        if (part.Body is not null &&
            (string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase)))
        {
            var identity = !string.IsNullOrWhiteSpace(part.Body.AttachmentId)
                ? part.Body.AttachmentId
                : $"part:{part.PartId ?? "root"}";

            results.Add(
                new GmailTextBodyCandidate(
                    part.MimeType!,
                    identity,
                    part.Body.AttachmentId,
                    part.Body.Data,
                    part.Body.Size));
        }

        foreach (var child in part.Parts)
            VisitText(child, results);
    }
}
