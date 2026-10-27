using Revestik.Api.Services.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyDocumentPdfChangeDetectorTests
{
    [Fact]
    public void IdenticalPreferencesProduceNoChanges()
    {
        var values = new CompanyDocumentPdfPreferences();
        Assert.Empty(CompanyDocumentPdfChangeDetector.GetChangedFields(values, values with { }));
    }

    [Fact]
    public void SingleChangeReturnsOnlyItsName()
    {
        var before = new CompanyDocumentPdfPreferences();
        var after = before with { ShowWebsite = false };
        Assert.Equal(["ShowWebsite"], CompanyDocumentPdfChangeDetector.GetChangedFields(before, after));
    }

    [Fact]
    public void SeveralChangesDoNotIncludeUnchangedValues()
    {
        var before = new CompanyDocumentPdfPreferences();
        var after = before with { FooterMessage = "Gracias", ShowQuotationSeller = false };
        Assert.Equal(["FooterMessage", "ShowQuotationSeller"],
            CompanyDocumentPdfChangeDetector.GetChangedFields(before, after));
    }

    [Fact]
    public void AllFieldsCanBeDetected()
    {
        var before = new CompanyDocumentPdfPreferences();
        var after = new CompanyDocumentPdfPreferences
        {
            FooterMessage = "Pie", QuotationDefaultTerms = "Condiciones",
            ShowWebsite = false, ShowQuotationSeller = false
        };
        Assert.Equal(4, CompanyDocumentPdfChangeDetector.GetChangedFields(before, after).Count);
    }
}
