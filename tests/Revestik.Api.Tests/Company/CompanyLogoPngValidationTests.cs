using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

// Pure unit tests: no SQL, Testcontainers, application host or external requests.
public sealed class CompanyLogoPngValidationTests
{
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    [Fact]
    public void ValidPngWithCorrectChunkCrc_IsAccepted()
    {
        Assert.Equal("image/png", CompanyLogoStorage.Validate(ValidPng, "logo.png"));
    }

    [Fact]
    public void CorruptPngDataChunk_IsRejected()
    {
        var corrupt = (byte[])ValidPng.Clone();
        corrupt[44] ^= 1;
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(corrupt, "logo.png"));
    }

    [Fact]
    public void AppendedDataAfterIend_IsRejected()
    {
        var invalid = new byte[ValidPng.Length + 1];
        ValidPng.CopyTo(invalid, 0);
        invalid[^1] = 42;
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(invalid, "logo.png"));
    }

    [Fact]
    public void TruncatedPng_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(ValidPng[..^1], "logo.png"));
    }
}
