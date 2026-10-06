namespace Revestik.Api.Services.GmailIntegration;

internal sealed record GmailXmlAttachmentCandidate(
    string FileName,
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
        Visit(root, results);
        return results;
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

    private static void Visit(
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
            Visit(child, results);
    }
}
