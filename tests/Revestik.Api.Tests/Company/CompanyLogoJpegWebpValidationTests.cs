using System.Buffers.Binary;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

// Byte-only validation: no SQL, HTTP host, or Testcontainers.
public sealed class CompanyLogoJpegWebpValidationTests
{
    [Fact]
    public void JpegWithFrameButWithoutScan_IsRejected()
    {
        byte[] jpeg = [
            0xff, 0xd8,
            0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xff, 0xd9
        ];
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(jpeg, "logo.jpg"));
    }

    [Fact]
    public void JpegWithEmptyScan_IsRejected()
    {
        byte[] jpeg = [
            0xff, 0xd8,
            0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
            0xff, 0xd9
        ];
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(jpeg, "logo.jpeg"));
    }

    [Fact]
    public void WebpVp8xCanvasWithoutImage_IsRejected()
    {
        byte[] webp = Riff("VP8X", new byte[10]);
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(webp, "logo.webp"));
    }

    [Fact]
    public void WebpWithTruncatedChunk_IsRejected()
    {
        byte[] webp = Riff("VP8 ", [0x00, 0x00, 0x00, 0x9d, 0x01, 0x2a, 0x01, 0x00, 0x01, 0x00]);
        BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(16, 4), 100);
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(webp, "logo.webp"));
    }

    [Fact]
    public void WebpWithExtraTrailingBytes_IsRejected()
    {
        byte[] webp = Riff("VP8 ", [0x00, 0x00, 0x00, 0x9d, 0x01, 0x2a, 0x01, 0x00, 0x01, 0x00]);
        Array.Resize(ref webp, webp.Length + 2);
        BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(4, 4), (uint)webp.Length - 8);
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(webp, "logo.webp"));
    }

    private static byte[] Riff(string chunk, byte[] payload)
    {
        var bytes = new byte[20 + payload.Length + (payload.Length & 1)];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), (uint)bytes.Length - 8);
        "WEBP"u8.CopyTo(bytes.AsSpan(8, 4));
        System.Text.Encoding.ASCII.GetBytes(chunk).CopyTo(bytes, 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), (uint)payload.Length);
        payload.CopyTo(bytes, 20);
        return bytes;
    }
}
