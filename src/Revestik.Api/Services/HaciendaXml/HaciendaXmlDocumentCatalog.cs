namespace Revestik.Api.Services.HaciendaXml;

public static class HaciendaXmlDocumentCatalog
{
    private const string BaseNamespace = "https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/";

    public static readonly IReadOnlyList<HaciendaXmlDocumentDescriptor> All =
    [
        new(HaciendaXmlDocumentKind.FacturaElectronica, "FacturaElectronica", BaseNamespace + "facturaElectronica", "FacturaElectronica_V4.4.xsd", true),
        new(HaciendaXmlDocumentKind.MensajeHacienda, "MensajeHacienda", BaseNamespace + "mensajeHacienda", "MensajeHacienda_V4.4.xsd", true),
        new(HaciendaXmlDocumentKind.MensajeReceptor, "MensajeReceptor", BaseNamespace + "mensajeReceptor", "MensajeReceptor_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.NotaCreditoElectronica, "NotaCreditoElectronica", BaseNamespace + "notaCreditoElectronica", "NotaCreditoElectronica_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.NotaDebitoElectronica, "NotaDebitoElectronica", BaseNamespace + "notaDebitoElectronica", "NotaDebitoElectronica_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.TiqueteElectronico, "TiqueteElectronico", BaseNamespace + "tiqueteElectronico", "TiqueteElectronico_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.FacturaElectronicaCompra, "FacturaElectronicaCompra", BaseNamespace + "facturaElectronicaCompra", "FacturaElectronicaCompra_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.FacturaElectronicaExportacion, "FacturaElectronicaExportacion", BaseNamespace + "facturaElectronicaExportacion", "FacturaElectronicaExportacion_V4.4.xsd", false),
        new(HaciendaXmlDocumentKind.ReciboElectronicoPago, "ReciboElectronicoPago", BaseNamespace + "reciboElectronicoPago", "ReciboElectronicoPago_V4.4.xsd", false)
    ];

    public static bool TryResolve(
        string rootElement,
        string xmlNamespace,
        out HaciendaXmlDocumentDescriptor descriptor)
    {
        descriptor = All.FirstOrDefault(item =>
            string.Equals(item.RootElement, rootElement, StringComparison.Ordinal) &&
            string.Equals(item.Namespace, xmlNamespace, StringComparison.Ordinal))!;

        return descriptor is not null;
    }
}
