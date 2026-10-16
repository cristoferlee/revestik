using System.IO.Compression;

namespace Revestik.Api.Services.Projects.Documents;

// Inspect actual signatures; neither the browser-supplied MIME nor extension is trusted.
public static class ProjectDocumentUploadPolicy
{
    public const long MaxUploadBytes = 20L * 1024 * 1024;

    public static async Task<ValidatedProjectUpload> ValidateAsync(
        Stream input, string fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 250 ||
            Path.GetFileName(fileName) != fileName || fileName.Contains('\\') ||
            fileName.Any(char.IsControl))
            throw new InvalidDataException("Nombre de archivo inválido.");
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (output.Length + read > MaxUploadBytes)
                throw new InvalidDataException("El archivo supera 20 MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (output.Length == 0) throw new InvalidDataException("Archivo vacío.");
        var bytes = output.ToArray();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = ext switch
        {
            ".jpg" or ".jpeg" when Jpeg(bytes) => "image/jpeg",
            ".png" when Png(bytes) => "image/png",
            ".webp" when Webp(bytes) => "image/webp",
            ".pdf" when Pdf(bytes) => "application/pdf",
            ".docx" when Ooxml(bytes, "word/document.xml") =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" when Ooxml(bytes, "xl/workbook.xml") =>
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => throw new InvalidDataException("El contenido no corresponde a un formato permitido.")
        };
        return new(bytes, mime);
    }
    private static bool Jpeg(byte[] b) => b.Length > 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff;
    private static bool Png(byte[] b) => b.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10});
    private static bool Webp(byte[] b) => b.Length > 12 && b.AsSpan(0,4).SequenceEqual("RIFF"u8) &&
        b.AsSpan(8,4).SequenceEqual("WEBP"u8);
    private static bool Pdf(byte[] b) => b.AsSpan().StartsWith("%PDF-"u8);
    private static bool Ooxml(byte[] b, string mainPart)
    {
        if (b.Length < 4 || b[0] != 0x50 || b[1] != 0x4b) return false;
        try
        {
            using var stream = new MemoryStream(b, writable:false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count > 2000) return false;
            if (archive.Entries.Any(e => e.Length > 64L * 1024 * 1024)) return false;
            return archive.GetEntry("[Content_Types].xml") is not null && archive.GetEntry(mainPart) is not null;
        }
        catch (InvalidDataException) { return false; }
        catch (IOException) { return false; }
    }
}
public sealed record ValidatedProjectUpload(byte[] Bytes, string ContentType);
