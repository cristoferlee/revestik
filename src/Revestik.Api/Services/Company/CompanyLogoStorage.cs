using System.Buffers.Binary;
using System.IO;

namespace Revestik.Api.Services.Company;

// Company assets have their own private storage; project evidence is not reused.
public sealed class CompanyLogoStorage(IHostEnvironment environment, IConfiguration configuration)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxDimension = 8192;
    private readonly string root = !environment.IsProduction()
        ? Path.Combine(environment.ContentRootPath, "App_Data", "company-logos")
        : Path.Combine(configuration["RevestikStorage:RootPath"]
            ?? throw new InvalidOperationException("Persistent storage is required."), "company-logos");

    public async Task<string> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N");
        var path = Path.Combine(root, key);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        try { await file.WriteAsync(bytes, ct); await file.FlushAsync(ct); }
        catch { file.Close(); File.Delete(path); throw; }
        return key;
    }

    public Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _))
            throw new InvalidDataException("Identificador de imagen inválido.");
        return File.ReadAllBytesAsync(Path.Combine(root, key), ct);
    }

    // Verify structural image framing, PNG chunk CRCs and dimensions.
    // This is not full image decoding; other formats still require hardened decoding validation.
    public static string Validate(ReadOnlySpan<byte> bytes, string fileName)
    {
        if (bytes.IsEmpty || bytes.Length > MaxBytes) throw new InvalidDataException("La imagen debe medir como máximo 2 MB.");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".png" && ValidPng(bytes)) return "image/png";
        if (ext is ".jpg" or ".jpeg" && ValidJpeg(bytes)) return "image/jpeg";
        if (ext == ".webp" && ValidWebp(bytes)) return "image/webp";
        throw new InvalidDataException("Adjuntá una imagen PNG, JPG o WebP válida.");
    }

    private static bool SizeOk(long w, long h) => w > 0 && h > 0 && w <= MaxDimension && h <= MaxDimension;

    private static bool ValidPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 57 || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return false;

        int offset = 8;
        bool hasHeader = false;
        bool hasData = false;
        bool hasEnd = false;
        while (offset <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) return false;
            int size = (int)length;
            var typeAndPayload = bytes.Slice(offset + 4, size + 4);
            uint expected = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + size, 4));
            if (PngCrc32(typeAndPayload) != expected) return false;
            var type = bytes.Slice(offset + 4, 4);
            var payload = bytes.Slice(offset + 8, size);

            if (!hasHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) || size != 13) return false;
                if (!SizeOk(BinaryPrimitives.ReadUInt32BigEndian(payload[..4]),
                    BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)))) return false;
                if (payload[10] != 0 || payload[11] != 0 || payload[12] > 1) return false;
                hasHeader = true;
            }
            else if (type.SequenceEqual("IHDR"u8)) return false;
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (size == 0) return false;
                hasData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (size != 0 || !hasData || offset + size + 12 != bytes.Length) return false;
                hasEnd = true;
                break;
            }
            offset += size + 12;
        }
        return hasHeader && hasData && hasEnd;
    }

    // PNG specifies CRC-32/ISO-HDLC over chunk type + chunk payload.
    private static uint PngCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffff;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    // Validate marker segment framing through SOS and require a nonempty encoded scan.
    // No attempt is made to decode the compressed image coefficients.
    private static bool ValidJpeg(ReadOnlySpan<byte> b)
    {
        if (b.Length < 20 || b[0] != 0xff || b[1] != 0xd8 || b[^2] != 0xff || b[^1] != 0xd9)
            return false;

        bool hasFrame = false;
        int offset = 2;
        while (offset < b.Length - 2)
        {
            if (b[offset++] != 0xff) return false;
            while (offset < b.Length - 2 && b[offset] == 0xff) offset++;
            if (offset >= b.Length - 2) return false;
            byte marker = b[offset++];
            if (marker is 0x00 or 0xd8 or 0xd9) return false;
            if (marker is >= 0xd0 and <= 0xd7 or 0x01) continue;
            if (offset + 2 > b.Length - 2) return false;
            int length = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset, 2));
            if (length < 2 || length > b.Length - 2 - offset) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
                or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                if (length < 8 || !SizeOk(BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset + 5, 2)),
                        BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset + 3, 2)))) return false;
                hasFrame = true;
            }
            if (marker == 0xda)
            {
                if (!hasFrame || length < 6) return false;
                offset += length;
                if (offset >= b.Length - 2) return false;
                // JPEG may contain multiple scans, restart markers and in-scan tables.
                // Requiring actual bytes after SOS catches header-only impostors
                // without rejecting normal progressive JPEGs.
                return b.Length - 2 - offset >= 2;
            }
            offset += length;
        }
        return false;
    }

    // Parse complete RIFF chunk framing; require a nonempty supported image payload.
    // This is deliberately not a WebP decoder.
    private static bool ValidWebp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 26 || !b[..4].SequenceEqual("RIFF"u8) || !b.Slice(8, 4).SequenceEqual("WEBP"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(4, 4)) != (uint)b.Length - 8)
            return false;

        bool hasImage = false;
        bool hasCanvas = false;
        int offset = 12;
        while (offset <= b.Length - 8)
        {
            int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(offset + 4, 4));
            if (length < 0 || (long)offset + 8 + length + (length & 1) > b.Length) return false;
            var type = b.Slice(offset, 4);
            var payload = b.Slice(offset + 8, length);
            if (type.SequenceEqual("VP8X"u8))
            {
                if (hasCanvas || offset != 12 || length != 10) return false;
                hasCanvas = SizeOk(1 + payload[4] + (payload[5] << 8) + (payload[6] << 16),
                    1 + payload[7] + (payload[8] << 8) + (payload[9] << 16));
                if (!hasCanvas) return false;
            }
            else if (type.SequenceEqual("VP8 "u8))
            {
                if (length < 10 || !payload.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a })
                    || !SizeOk(BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2)) & 0x3fff,
                        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(8, 2)) & 0x3fff)) return false;
                hasImage = true;
            }
            else if (type.SequenceEqual("VP8L"u8))
            {
                if (length < 5 || payload[0] != 0x2f) return false;
                int w = 1 + (payload[1] | ((payload[2] & 0x3f) << 8));
                int h = 1 + ((payload[2] >> 6) | (payload[3] << 2) | ((payload[4] & 15) << 10));
                if (!SizeOk(w, h)) return false;
                hasImage = true;
            }
            else if (type.SequenceEqual("ANIM"u8))
            {
                if (!hasCanvas || length != 6) return false;
            }
            else if (type.SequenceEqual("ANMF"u8))
            {
                if (!hasCanvas || length <= 16) return false;
                hasImage = true;
            }
            offset += 8 + length + (length & 1);
        }
        return offset == b.Length && hasImage;
    }
}
