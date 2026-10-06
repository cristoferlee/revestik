using System.Text;
using System.Xml.Schema;
using Revestik.Api.Services.HaciendaXml;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class HaciendaXmlPlatformTests
{
    [Theory]
    [InlineData("FacturaElectronica", "facturaElectronica", true)]
    [InlineData("MensajeHacienda", "mensajeHacienda", true)]
    [InlineData("MensajeReceptor", "mensajeReceptor", false)]
    [InlineData("NotaCreditoElectronica", "notaCreditoElectronica", false)]
    [InlineData("NotaDebitoElectronica", "notaDebitoElectronica", false)]
    [InlineData("TiqueteElectronico", "tiqueteElectronico", false)]
    [InlineData("FacturaElectronicaCompra", "facturaElectronicaCompra", false)]
    [InlineData("FacturaElectronicaExportacion", "facturaElectronicaExportacion", false)]
    [InlineData("ReciboElectronicoPago", "reciboElectronicoPago", false)]
    public void Detect_OfficialV44Root_ResolvesDescriptor(
        string root,
        string namespaceSuffix,
        bool processingEnabled)
    {
        var xml = $"<{{root}} xmlns=\"https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/{{namespaceSuffix}}\" />";
        xml = xml.Replace("{root}", root).Replace("{namespaceSuffix}", namespaceSuffix);

        var descriptor = HaciendaXmlDocumentDetector.Detect(Encoding.UTF8.GetBytes(xml));

        Assert.Equal(root, descriptor.RootElement);
        Assert.Equal(processingEnabled, descriptor.ProcessingEnabled);
    }

    [Fact]
    public void Detect_OldVersion_RejectsDocument()
    {
        const string xml = "<FacturaElectronica xmlns=\"https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.3/facturaElectronica\" />";
        Assert.Throws<HaciendaXmlValidationException>(() =>
            HaciendaXmlDocumentDetector.Detect(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Detect_FakeNamespace_RejectsDocument()
    {
        const string xml = "<FacturaElectronica xmlns=\"https://attacker.example/facturaElectronica\" />";
        Assert.Throws<HaciendaXmlValidationException>(() =>
            HaciendaXmlDocumentDetector.Detect(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Detect_Dtd_RejectsDocument()
    {
        const string xml = "<!DOCTYPE FacturaElectronica [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><FacturaElectronica xmlns=\"https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica\">&xxe;</FacturaElectronica>";
        Assert.Throws<HaciendaXmlValidationException>(() =>
            HaciendaXmlDocumentDetector.Detect(Encoding.UTF8.GetBytes(xml)));
    }

    [Theory]
    [InlineData(XmlSeverityType.Warning, false)]
    [InlineData(XmlSeverityType.Error, true)]
    public void SchemaValidation_OnlyErrorsRejectDocument(
        XmlSeverityType severity,
        bool expected)
    {
        Assert.Equal(expected, HaciendaXmlSchemaValidator.ShouldRejectSchemaIssue(severity));
    }

    [Fact]
    public void Catalog_HasUniqueRootAndNamespacePairs()
    {
        var duplicate = HaciendaXmlDocumentCatalog.All
            .GroupBy(item => (item.RootElement, item.Namespace))
            .FirstOrDefault(group => group.Count() > 1);

        Assert.Null(duplicate);
    }
}
