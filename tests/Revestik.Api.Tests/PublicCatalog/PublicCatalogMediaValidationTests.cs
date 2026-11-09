using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Services.Company;
using System.Buffers.Binary;
using Xunit;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogMediaValidationTests
{
    [Fact]
    public void RejectsEmptyImage()
    {
        Assert.Throws<InvalidDataException>(() => PublicCatalogMediaValidation.Validate([],"image.png","ProductCover"));
    }

    [Fact]
    public void RejectsOversizedFile()
    {
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(new byte[5*1024*1024+1],"image.png","ProductCover"));
    }

    [Theory]
    [InlineData("ProductCover")]
    [InlineData("ProductGallery")]
    [InlineData("CategoryGallery")]
    [InlineData("CategoryHeroFallback")]
    public void RejectsGifOutsideMainHero(string role)
    {
        var gif=System.Text.Encoding.ASCII.GetBytes("GIF89aThisIsNotAnActualGif");
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(gif,"image.gif",role));
    }

    [Fact]
    public void AcceptsWellFormedTwoFrameHeroGif()
    {
        var bytes=Convert.FromBase64String("R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAGQAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQBGQABACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7");
        var result=PublicCatalogMediaValidation.Validate(bytes,"hero.gif","CategoryHeroPrimary");
        Assert.Equal("image/gif",result.ContentType);
        Assert.Equal(2,result.FrameCount);
        Assert.Equal(2,result.Width);
        Assert.Equal(2,result.Height);
    }

    [Theory]
    [InlineData("ProductCover")]
    [InlineData("ProductGallery")]
    public void AcceptsValidPngLargerThanLogoLimit(string role)
    {
        var png = MakePngWithAncillaryText(3 * 1024 * 1024);
        var media = PublicCatalogMediaValidation.Validate(png, "product.png", role);
        Assert.Equal("image/png", media.ContentType);
        Assert.Equal(1, media.Width);
        Assert.Equal(1, media.Height);
        Assert.Equal((long)png.Length, media.FileSizeBytes);
    }

    [Fact]
    public void CompanyLogoStillRejectsFilesOverTwoMegabytes()
    {
        var png = MakePngWithAncillaryText(3 * 1024 * 1024);
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(png, "company.png"));
    }

    [Fact]
    public void RejectsLargePngWithCorruptAncillaryChecksum()
    {
        var png = MakePngWithAncillaryText(3 * 1024 * 1024);
        png[^18] ^= 0x01;
        Assert.Throws<InvalidDataException>(() => PublicCatalogMediaValidation.Validate(png, "product.png", "ProductCover"));
    }

    // An ordinary, decodable PNG with a large ancillary text chunk. The image
    // itself is one pixel; no high-resolution decompression burden is added.
    private static byte[] MakePngWithAncillaryText(int textLength)
    {
        var tiny = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNg+M/wHwAEAQH/cetH5QAAAABJRU5ErkJggg==");
        var ancillary = new byte[textLength];
        var label = "Comment\0"u8;
        label.CopyTo(ancillary);
        ancillary.AsSpan(label.Length).Fill((byte)'x');
        var png = new byte[tiny.Length + textLength + 12];
        // Insert tEXt before IEND, keeping the original valid IHDR and IDAT.
        var offset = tiny.Length - 12;
        tiny.AsSpan(0, offset).CopyTo(png);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(offset, 4), (uint)ancillary.Length);
        "tEXt"u8.CopyTo(png.AsSpan(offset + 4));
        ancillary.AsSpan().CopyTo(png.AsSpan(offset + 8));
        var crc = 0xffffffffu;
        foreach (var b in png.AsSpan(offset + 4, textLength + 4))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(offset + 8 + textLength, 4), ~crc);
        tiny.AsSpan(offset).CopyTo(png.AsSpan(offset + textLength + 12));
        return png;
    }

    [Fact]
    public void RejectsForgedImageExtension()
    {
        Assert.Throws<InvalidDataException>(()=>PublicCatalogMediaValidation.Validate(System.Text.Encoding.ASCII.GetBytes("hello"),"image.webp","ProductCover"));
    }
}
