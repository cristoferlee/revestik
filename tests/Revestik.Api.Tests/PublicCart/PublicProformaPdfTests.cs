using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Infrastructure;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicProformaPdfTests
{
    [Fact]
    public void Pdf_IsLetterSizeAndStartsWithPdfHeader()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = new PublicProformaPdfService().Generate("es",
            [new PublicProformaLine(1, "Porcelanato gris", "REVESTIK", 2.5m,
                "m²", 12000m, "CRC", null)], DateTimeOffset.UtcNow);
        Assert.True(pdf.Length > 100);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        AssertAllPagesAreLetter(pdf, minimumPages: 1);
    }

    [Fact]
    public void Pdf_SupportsMultiplePagesAndMixedCurrenciesWithoutImages()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var lines = Enumerable.Range(1, 50)
            .Select(i => new PublicProformaLine(i, "Producto de acabado " + i + " con descripción de referencia",
                "Marca", 3m, "m²", i % 4 == 0 ? null : 120m,
                i % 2 == 0 ? "USD" : "CRC", null)).ToArray();
        var pdf = new PublicProformaPdfService().Generate("en", lines, DateTimeOffset.UtcNow);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        AssertAllPagesAreLetter(pdf, minimumPages: 2);
    }

    [Fact]
    public void Pdf_WithValidCoverAndCorruptCover_RemainsPrintable()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var cover = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAFElEQVR4nGPcVGHDgA0wYRUdtBIAHHEBdvW0Ot8AAAAASUVORK5CYII=");
        var lines = new[]
        {
            new PublicProformaLine(1, "Producto con portada", null, 1m, "m²", 100m, "CRC", cover),
            new PublicProformaLine(2, "Producto con imagen corrupta", null, 1m, "m²", null, null,
                [0x89, 0x50, 0x4E, 0x47, 0x00]),
        };
        var pdf = new PublicProformaPdfService().Generate("es", lines, DateTimeOffset.UtcNow);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        AssertAllPagesAreLetter(pdf, minimumPages: 1);
    }

    [Fact]
    public void Pdf_RejectsEmptySelection()
    {
        Assert.Throws<ArgumentException>(() => new PublicProformaPdfService()
            .Generate("es", [], DateTimeOffset.UtcNow));
    }

    private static void AssertAllPagesAreLetter(byte[] pdf, int minimumPages)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var pages = Regex.Matches(text, @"/Type\s*/Page\b").Count;
        Assert.True(pages >= minimumPages,
            $"Expected at least {minimumPages} PDF page objects; found {pages}.");

        // Check every explicit MediaBox, not just the first page. Skia's PDF
        // writer sets a MediaBox on each page; absence is a test failure.
        var boxes = Regex.Matches(text, @"/MediaBox\s*\[[^\]]+\]");
        Assert.Equal(pages, boxes.Count);
        foreach (Match box in boxes)
            Assert.Matches(new Regex(
                @"/MediaBox\s*\[\s*0(?:\.0+)?\s+0(?:\.0+)?\s+612(?:\.0+)?\s+792(?:\.0+)?\s*\]"),
                box.Value);
    }
}
