using System.IO.Compression;
using Revestik.Api.Services.Projects.Documents;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDocumentUploadPolicySecurityTests
{
    [Fact]
    public async Task WordDocument_WithMinimalStructuralParts_IsAccepted()
    {
        var data = BuildArchive(archive =>
        {
            Write(archive, "[Content_Types].xml", "<Types />");
            Write(archive, "word/document.xml", "<document />");
        });
        using var stream = new MemoryStream(data);
        var result = await ProjectDocumentUploadPolicy.ValidateAsync(
            stream, "example.docx", CancellationToken.None);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
    }

    [Fact]
    public async Task WordDocument_WithExcessiveCombinedUncompressedSize_IsRejected()
    {
        var data = BuildArchive(archive =>
        {
            Write(archive, "[Content_Types].xml", "<Types />");
            Write(archive, "word/document.xml", "<document />");
            for (int i = 0; i < 3; i++)
            {
                var entry = archive.CreateEntry($"assets/filler{i}.bin", CompressionLevel.Optimal);
                using var output = entry.Open();
                var zeros = new byte[1024 * 1024];
                for (int block = 0; block < 50; block++) output.Write(zeros);
            }
        });
        using var stream = new MemoryStream(data);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProjectDocumentUploadPolicy.ValidateAsync(stream, "example.docx", CancellationToken.None));
    }

    private static byte[] BuildArchive(Action<ZipArchive> fill)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            fill(archive);
        return output.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }
}
