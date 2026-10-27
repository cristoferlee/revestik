using System.IO;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyDocumentPdfPreferencesSnapshotSerializerTests
{
    [Fact]
    public void RoundTripPreservesPreferences()
    {
        var original = new CompanyDocumentPdfPreferences
        {
            FooterMessage = "Gracias por su compra",
            QuotationDefaultTerms = "Válida según disponibilidad",
            ShowWebsite = false,
            ShowQuotationSeller = false
        };
        var json = CompanyDocumentPdfPreferencesSnapshotSerializer.Serialize(original);
        Assert.Equal(original, CompanyDocumentPdfPreferencesSnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void NullIsLegacyDocument()
    {
        Assert.Null(CompanyDocumentPdfPreferencesSnapshotSerializer.Deserialize(null));
    }

    [Fact]
    public void DefaultsPreserveExistingVisibility()
    {
        var defaults = new CompanyDocumentPdfPreferences();
        Assert.True(defaults.ShowWebsite);
        Assert.True(defaults.ShowQuotationSeller);
        Assert.Empty(defaults.FooterMessage);
        Assert.Empty(defaults.QuotationDefaultTerms);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"footerMessage\":\"\",\"quotationDefaultTerms\":\"\"}")]
    public void RejectsInvalidOrIncompleteSnapshots(string json)
    {
        Assert.Throws<InvalidDataException>(() =>
            CompanyDocumentPdfPreferencesSnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void RejectsOversizedText()
    {
        Assert.Throws<ArgumentException>(() =>
            CompanyDocumentPdfPreferencesSnapshotSerializer.Serialize(
                new CompanyDocumentPdfPreferences { FooterMessage = new string('x', 501) }));
    }
}
