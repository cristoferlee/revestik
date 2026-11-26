using SkiaSharp;

namespace Revestik.Api.Services.PublicCatalog;

internal static class PublicImageOptimizer
{
    internal static SKData Encode(Stream input, int maxSide)
    {
        using var codec = SKCodec.Create(input);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or
            SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Gif))
            throw new InvalidDataException("Unsupported image format.");

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 24_000_000)
            throw new InvalidDataException("Image dimensions are outside safety limits.");

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var source = new SKBitmap(info);
        if (codec.GetPixels(info, source.GetPixels(), new SKCodecOptions(0)) != SKCodecResult.Success)
            throw new InvalidDataException("The image could not be decoded.");

        var scale = Math.Min(1d, (double)maxSide / Math.Max(width, height));
        var w = Math.Max(1, (int)Math.Round(width * scale));
        var h = Math.Max(1, (int)Math.Round(height * scale));
        var origin = codec.EncodedOrigin;
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var output = new SKBitmap(swapped ? h : w, swapped ? w : h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);
        var transform = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.SetMatrix(transform);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Linear));
        canvas.Flush();
        return output.Encode(SKEncodedImageFormat.Webp, 82)
            ?? throw new InvalidDataException("The image could not be encoded.");
    }
}
