using Revestik.Api.Services.PublicCatalog;
using SkiaSharp;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicImageOptimizerTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Encode_ResizesAndPreservesAspectRatio(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(320, 160);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var input = bitmap.Encode(format, 90);
        using var stream = input.AsStream();
        using var output = PublicImageOptimizer.Encode(stream, 160);
        using var encoded = output.AsStream();
        using var codec = SKCodec.Create(encoded);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal((160, 80), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public void Encode_PreservesTransparencyWithoutUpscaling()
    {
        using var bitmap = new SKBitmap(8, 4);
        bitmap.Erase(SKColors.Transparent);
        using var input = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = input.AsStream();
        using var output = PublicImageOptimizer.Encode(stream, 1920);
        using var result = SKBitmap.Decode(output);
        Assert.Equal((8, 4), (result.Width, result.Height));
        Assert.Equal((byte)0, result.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void Encode_RejectsUnableToDecodeInput()
    {
        using var stream = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        Assert.Throws<InvalidDataException>(() => PublicImageOptimizer.Encode(stream, 1920));
    }

    [Fact]
    public void OptimizedWebpCanBeDecodedByQuestPdf()
    {
        using var bitmap = new SKBitmap(8, 4);
        bitmap.Erase(SKColors.Orange);
        using var input = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = input.AsStream();
        using var output = PublicImageOptimizer.Encode(stream, 1920);
        using var image = QuestPDF.Infrastructure.Image.FromBinaryData(output.ToArray());
        Assert.NotNull(image);
    }

    [Fact]
    public void Encode_ConvertsAnimatedGifToStaticWebp()
    {
        using var input = new MemoryStream(Convert.FromBase64String(
            "R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAGQAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQBGQABACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7"));
        using var output = PublicImageOptimizer.Encode(input, 1920);
        using var encoded = output.AsStream();
        using var codec = SKCodec.Create(encoded);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.True(codec.FrameCount <= 1);
        Assert.Equal((2, 2), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public void Encode_AppliesExifOrientationAndDoesNotCarryExifForward()
    {
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.Blue);
        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        var bytes = jpeg.ToArray();
        // JPEG APP1 marker, little-endian TIFF, and one Orientation=6 entry.
        byte[] exif = [
            0xff, 0xe1, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0,
            8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0
        ];
        using var input = new MemoryStream([.. bytes[..2], .. exif, .. bytes[2..]]);
        using var output = PublicImageOptimizer.Encode(input, 1920);
        using var encoded = output.AsStream();
        using var codec = SKCodec.Create(encoded);
        Assert.Equal((8, 12), (codec.Info.Width, codec.Info.Height));
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
        Assert.DoesNotContain("EXIF", System.Text.Encoding.ASCII.GetString(output.ToArray()));
    }
}
