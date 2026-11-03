using System.Buffers.Binary;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Services.PublicCatalog;

public static class PublicCatalogMediaValidation
{
    public static PublicCatalogValidatedMedia Validate(byte[] bytes, string filename, string role, bool detectFromBytes = false)
    {
        if (bytes.Length == 0 || bytes.Length > PublicCatalogMediaStorage.MaxBytes)
            throw new InvalidDataException("Image must be between 1 byte and 5 MB.");
        if (role is not ("ProductCover" or "ProductGallery" or "CategoryHeroPrimary" or "CategoryHeroFallback" or "CategoryGallery"))
            throw new InvalidDataException("Invalid image role.");
        var mime = Identify(bytes);
        if (mime == "image/gif")
        {
            if (role != "CategoryHeroPrimary") throw new InvalidDataException("Animated GIF is permitted only as category hero.");
            if (!detectFromBytes && !filename.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Extension and image content do not match.");
            var (w, h, frames) = ValidateGif(bytes);
            return new(mime, w, h, bytes.LongLength, frames);
        }
        var ext = mime switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException("Unsupported image format.")
        };
        if (!detectFromBytes)
        {
            var supplied = Path.GetExtension(filename).ToLowerInvariant();
            if (supplied != ext && !(mime == "image/jpeg" && supplied == ".jpeg"))
                throw new InvalidDataException("Extension and image content do not match.");
        }
        // Structural checks already used for the company's logo. This does NOT
        // replace full image decoding or sanitization for untrusted image bytes.
        CompanyLogoStorage.Validate(bytes, "image" + ext);
        var (width, height) = mime switch
        {
            "image/png" => (checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4))),
                checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)))),
            "image/webp" => WebpSize(bytes),
            _ => JpegSize(bytes)
        };
        CheckSize(width, height);
        return new(mime, width, height, bytes.LongLength, 1);
    }

    private static string Identify(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})) return "image/png";
        if (b.Length >= 2 && b[0] == 0xff && b[1] == 0xd8) return "image/jpeg";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8,4).SequenceEqual("WEBP"u8)) return "image/webp";
        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8))) return "image/gif";
        throw new InvalidDataException("Unsupported image signature.");
    }

    private static void CheckSize(int w, int h)
    {
        if (w is < 1 or > 8192 || h is < 1 or > 8192 || (long)w * h > 24_000_000)
            throw new InvalidDataException("Image dimensions are outside safety limits.");
    }

    private static (int,int) JpegSize(byte[] b)
    {
        var p = 2;
        while (p < b.Length - 10)
        {
            if (b[p++] != 0xff) throw new InvalidDataException("Invalid JPEG markers.");
            while (p < b.Length && b[p] == 0xff) p++;
            if (p >= b.Length) break;
            var marker = b[p++];
            if (marker is >= 0xd0 and <= 0xd7 or 0x01) continue;
            if (marker is 0xd9 or 0xda) break;
            if (p + 2 > b.Length) break;
            var len = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p,2));
            if (len < 2 || p + len > b.Length) break;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
                return (BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p+5,2)), BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p+3,2)));
            p += len;
        }
        throw new InvalidDataException("JPEG size metadata is missing.");
    }

    private static (int,int) WebpSize(byte[] b)
    {
        var tag = System.Text.Encoding.ASCII.GetString(b,12,4);
        if (tag == "VP8X" && b.Length >= 30)
            return (1+b[24]+(b[25]<<8)+(b[26]<<16), 1+b[27]+(b[28]<<8)+(b[29]<<16));
        if (tag == "VP8 " && b.Length >= 30)
            return (BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(26,2)) & 0x3fff,
                BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(28,2)) & 0x3fff);
        if (tag == "VP8L" && b.Length >= 25)
            return (1+(b[21]|((b[22]&0x3f)<<8)), 1+((b[22]>>6)|(b[23]<<2)|((b[24]&15)<<10)));
        throw new InvalidDataException("Unsupported WebP framing.");
    }

    private static (int,int,int) ValidateGif(byte[] b)
    {
        if (b.Length < 20 || !b.AsSpan(0,6).SequenceEqual("GIF89a"u8))
            throw new InvalidDataException("Invalid animated GIF header.");
        int w=BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(6,2));
        int h=BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(8,2));
        CheckSize(w,h);
        int p=13, frames=0;
        int palette=(b[10]&0x80)!=0 ? 3*(1<<((b[10]&7)+1)) : 0;
        p+=palette;
        if (p>=b.Length) throw new InvalidDataException("Invalid GIF palette.");
        while (p<b.Length)
        {
            byte block=b[p++];
            if (block==0x3b)
            {
                if (p!=b.Length || frames<2 || frames>120) throw new InvalidDataException("GIF requires 2 to 120 valid frames.");
                return (w,h,frames);
            }
            if (block==0x21)
            {
                if (p>=b.Length) break;
                p++; // extension label
                SkipSubBlocks(b,ref p);
            }
            else if(block==0x2c)
            {
                if (p+9>b.Length) break;
                int fw=BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p+4,2));
                int fh=BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p+6,2));
                CheckSize(fw,fh);
                var packed=b[p+8];
                p+=9;
                if((packed&0x80)!=0) p+=3*(1<<((packed&7)+1));
                if(p>=b.Length) break;
                p++; // LZW minimum code size
                SkipSubBlocks(b,ref p);
                frames++;
                if(frames>120 || (long)frames*w*h>250_000_000) throw new InvalidDataException("GIF animation is too complex.");
            }
            else break;
        }
        throw new InvalidDataException("Invalid or incomplete GIF stream.");
    }

    private static void SkipSubBlocks(byte[] b,ref int p)
    {
        while(p<b.Length)
        {
            var size=b[p++];
            if(size==0) return;
            if(p+size>b.Length) break;
            p+=size;
        }
        throw new InvalidDataException("Invalid image data blocks.");
    }
}
