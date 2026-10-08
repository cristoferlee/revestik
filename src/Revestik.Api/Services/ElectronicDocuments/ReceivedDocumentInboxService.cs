using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ReceivedDocumentInboxService : IReceivedDocumentInboxService
{
    private const int MaxXmlBytes = 5 * 1024 * 1024;
    private const string ProtectorPurpose = "Revestik.ReceivedDocuments.Inbox.v1";
    private readonly RevestikDbContext dbContext;
    private readonly IElectronicDocumentXmlParser parser;
    private readonly IHaciendaXmlSchemaValidator schemaValidator;
    private readonly IElectronicDocumentImportService importService;
    private readonly CompanyOptions company;
    private readonly ReceivedDocumentInboxOptions options;
    private readonly IDataProtector protector;
    private readonly string storagePath;

    public ReceivedDocumentInboxService(
        RevestikDbContext dbContext,
        IElectronicDocumentXmlParser parser,
        IHaciendaXmlSchemaValidator schemaValidator,
        IElectronicDocumentImportService importService,
        IOptions<CompanyOptions> companyOptions,
        IOptions<ReceivedDocumentInboxOptions> inboxOptions,
        IDataProtectionProvider dataProtectionProvider,
        IWebHostEnvironment environment)
    {
        this.dbContext = dbContext;
        this.parser = parser;
        this.schemaValidator = schemaValidator;
        this.importService = importService;
        company = companyOptions.Value;
        options = inboxOptions.Value;
        protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        storagePath = Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(environment.ContentRootPath, options.StoragePath);
        Directory.CreateDirectory(storagePath);
    }

    public async Task<ReceivedDocumentInboxItemResponse> StageAsync(
        byte[] xml, string fileName, string source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (xml.Length == 0)
            throw new ReceivedDocumentInboxException("El archivo XML está vacío.");
        if (xml.Length > MaxXmlBytes)
            throw new ReceivedDocumentInboxException("El archivo XML no puede superar 5 MB.");
        if (!string.Equals(Path.GetExtension(fileName), ".xml", StringComparison.OrdinalIgnoreCase))
            throw new ReceivedDocumentInboxException("Solo se permiten archivos con extensión .xml.");

        HaciendaXmlValidationResult validated;
        try { validated = schemaValidator.Validate(xml); }
        catch (HaciendaXmlValidationException ex)
        { throw new ReceivedDocumentInboxException(ex.Message); }

        ReceivedDocumentInboxEnvelope envelope;
        if (!validated.Descriptor.ProcessingEnabled && !IsSupportedFiscalKind(validated.Descriptor.Kind))
        {
            envelope = BuildRecognizedNotEnabledEnvelope(validated.Descriptor, xml, fileName, source);
        }
        else
        {
            ParsedReceivedXml parsed;
            try { parsed = parser.Parse(xml); }
            catch (ElectronicDocumentXmlException ex)
            { throw new ReceivedDocumentInboxException(ex.Message); }

            envelope = parsed switch
            {
                ParsedReceivedElectronicDocument invoice =>
                    await BuildInvoiceEnvelopeAsync(invoice.Document, xml, fileName, source, cancellationToken),
                ParsedReceivedHaciendaResponse response =>
                    await BuildHaciendaEnvelopeAsync(response.Response, xml, fileName, source, cancellationToken),
                ParsedHaciendaDocument fiscal when IsSupportedFiscalKind(fiscal.Kind) =>
                    await BuildFiscalEnvelopeAsync(fiscal, xml, fileName, source, cancellationToken),
                _ => throw new ReceivedDocumentInboxException("Tipo XML recibido no soportado.")
            };
        }

        await SaveAsync(envelope, cancellationToken);
        return Map(envelope);
    }

    public async Task<IReadOnlyList<ReceivedDocumentInboxItemResponse>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var result = new List<ReceivedDocumentInboxItemResponse>();
        foreach (var path in Directory.EnumerateFiles(storagePath, "*.inbox"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await ReadAsync(path, cancellationToken);
            if (item.Status is ReceivedDocumentInboxStatus.PendingReview
                or ReceivedDocumentInboxStatus.Duplicate
                or ReceivedDocumentInboxStatus.RecognizedNotEnabled)
                result.Add(Map(item));
        }
        return result.OrderByDescending(x => x.ReceivedAtUtc).ToList();
    }

    public async Task<ReceivedDocumentInboxAcceptResponse> AcceptAsync(
        Guid inboxId, string userId, CancellationToken cancellationToken)
    {
        var path = GetPath(inboxId);
        if (!File.Exists(path)) throw new KeyNotFoundException("El documento pendiente no existe.");
        var item = await ReadAsync(path, cancellationToken);
        if (item.Status == ReceivedDocumentInboxStatus.Duplicate)
            throw new ReceivedDocumentInboxException("El documento es duplicado y no puede aceptarse nuevamente.");
        if (item.Status == ReceivedDocumentInboxStatus.RecognizedNotEnabled)
            throw new ReceivedDocumentInboxException("El documento fue recibido mientras su procesamiento estaba deshabilitado. Debe volver a ingresarse y validarse.");
        if (item.Status != ReceivedDocumentInboxStatus.PendingReview)
            throw new ReceivedDocumentInboxException("El documento ya no está pendiente de revisión.");

        var xml = Convert.FromBase64String(item.XmlBase64);
        // Always re-check the *current* schema and allowlist; a stored envelope isn't authorization.
        HaciendaXmlValidationResult validation;
        try { validation = schemaValidator.Validate(xml); }
        catch (HaciendaXmlValidationException ex)
        { throw new ReceivedDocumentInboxException(ex.Message); }
        if ((!validation.Descriptor.ProcessingEnabled && !IsSupportedFiscalKind(validation.Descriptor.Kind)) ||
            !string.Equals(validation.Descriptor.RootElement, item.DocumentKind, StringComparison.Ordinal))
            throw new ReceivedDocumentInboxException("El tipo fiscal no está habilitado o no coincide con el documento preparado.");

        try
        {
            var imported = await importService.ImportAsync(xml, userId, cancellationToken);
            item.Status = ReceivedDocumentInboxStatus.Accepted;
            item.AcceptedElectronicDocumentId = imported.ElectronicDocumentId ?? imported.Id;
            item.XmlBase64 = string.Empty;
            await SaveAsync(item, cancellationToken);
            return new ReceivedDocumentInboxAcceptResponse(item.Id, imported);
        }
        catch (DuplicateElectronicDocumentException ex)
        {
            item.Status = ReceivedDocumentInboxStatus.Duplicate;
            item.ExistingElectronicDocumentId = ex.ExistingId;
            await SaveAsync(item, cancellationToken);
            throw new ReceivedDocumentInboxException("El documento ya existe en Revestik.");
        }
    }

    public async Task<bool> RejectAsync(Guid inboxId, CancellationToken cancellationToken)
    {
        var path = GetPath(inboxId);
        if (!File.Exists(path)) return false;
        var item = await ReadAsync(path, cancellationToken);
        item.Status = ReceivedDocumentInboxStatus.Rejected;
        item.XmlBase64 = string.Empty;
        await SaveAsync(item, cancellationToken);
        return true;
    }

    private static bool IsSupportedFiscalKind(HaciendaXmlDocumentKind kind) => kind is
        HaciendaXmlDocumentKind.NotaCreditoElectronica or
        HaciendaXmlDocumentKind.NotaDebitoElectronica or
        HaciendaXmlDocumentKind.TiqueteElectronico or
        HaciendaXmlDocumentKind.FacturaElectronicaCompra or
        HaciendaXmlDocumentKind.FacturaElectronicaExportacion or
        HaciendaXmlDocumentKind.ReciboElectronicoPago;

    private async Task<ReceivedDocumentInboxEnvelope> BuildFiscalEnvelopeAsync(
        ParsedHaciendaDocument document, byte[] xml, string fileName, string source, CancellationToken cancellationToken)
    {
        var issuerMatches = MatchesCompany(document.Issuer.IdentificationType, document.Issuer.Identification);
        var receiverMatches = document.Receiver is not null &&
            MatchesCompany(document.Receiver.IdentificationType, document.Receiver.Identification);
        if (document.Receiver is not null && !receiverMatches && !issuerMatches)
            throw new ReceivedDocumentInboxException("El comprobante está dirigido a otra entidad fiscal.");
        if (!issuerMatches && !receiverMatches)
            throw new ReceivedDocumentInboxException("No es posible verificar la relación fiscal del comprobante con Revestik.");
        if (document.FiscalTotals is null || document.FiscalLines is null ||
            document.FiscalTotals.TotalDocument != document.TotalDocument)
            throw new ReceivedDocumentInboxException("El documento no contiene un desglose fiscal completo.");
        if (document.TotalDocument < 0 || (document.ExchangeRate.HasValue && document.ExchangeRate.Value <= 0))
            throw new ReceivedDocumentInboxException("El documento contiene importes o tipo de cambio inválidos.");
        if ((document.Kind is HaciendaXmlDocumentKind.NotaCreditoElectronica or
            HaciendaXmlDocumentKind.NotaDebitoElectronica or
            HaciendaXmlDocumentKind.FacturaElectronicaCompra or
            HaciendaXmlDocumentKind.ReciboElectronicoPago) &&
            document.References.Count is < 1 or > 10)
            throw new ReceivedDocumentInboxException("Este documento requiere entre 1 y 10 referencias fiscales.");
        if (document.References.Count > 10)
            throw new ReceivedDocumentInboxException("El documento supera diez referencias fiscales.");

        var exists = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == document.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var totalCrc = document.CurrencyCode.Equals("CRC", StringComparison.OrdinalIgnoreCase)
            ? document.TotalDocument
            : document.TotalDocument * (document.ExchangeRate ?? 1m);
        var high = totalCrc > options.HighAmountReviewThresholdCrc;
        var warnings = new List<string>();
        if (high) warnings.Add($"Monto extraordinariamente alto: equivalente aproximado CRC {totalCrc:N2}. Requiere revisión manual.");
        if (document.Receiver is null) warnings.Add("Sin receptor identificado. No se presume que sea un gasto deducible.");
        if (document.Kind is HaciendaXmlDocumentKind.NotaCreditoElectronica or HaciendaXmlDocumentKind.NotaDebitoElectronica)
            warnings.Add("Ajuste fiscal pendiente de vinculación y aprobación. No aplica movimientos económicos.");
        if (document.Kind == HaciendaXmlDocumentKind.ReciboElectronicoPago)
            warnings.Add("Comprobante de pago: no debe registrarse como un gasto adicional.");
        if (document.Kind is HaciendaXmlDocumentKind.FacturaElectronicaCompra or HaciendaXmlDocumentKind.FacturaElectronicaExportacion)
            warnings.Add("Evidencia fiscal sin clasificación económica automática.");
        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(), Source = source, FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = exists.HasValue ? ReceivedDocumentInboxStatus.Duplicate : ReceivedDocumentInboxStatus.PendingReview,
            DocumentKind = document.Kind.ToString(), Clave = document.Clave,
            IssuerName = document.Issuer.Name, IssuerIdentification = document.Issuer.Identification,
            ReceiverName = document.Receiver?.Name ?? string.Empty, CurrencyCode = document.CurrencyCode,
            Total = document.TotalDocument, TotalCrcEquivalent = totalCrc,
            RequiresHighAmountReview = high, Warnings = warnings,
            ExistingElectronicDocumentId = exists, XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private static ReceivedDocumentInboxEnvelope BuildRecognizedNotEnabledEnvelope(
        HaciendaXmlDocumentDescriptor descriptor, byte[] xml, string fileName, string source)
    {
        var preview = HaciendaXmlPreviewReader.Read(xml);
        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(), Source = source, FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow, Status = ReceivedDocumentInboxStatus.RecognizedNotEnabled,
            DocumentKind = descriptor.RootElement, Clave = preview.Clave,
            IssuerName = preview.IssuerName, IssuerIdentification = preview.IssuerIdentification,
            ReceiverName = preview.ReceiverName, CurrencyCode = preview.CurrencyCode,
            Total = preview.Total, TotalCrcEquivalent = preview.Total,
            RequiresHighAmountReview = false,
            Warnings = ["XML Hacienda reconocido, pero todavía no habilitado para aceptación."],
            XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private async Task<ReceivedDocumentInboxEnvelope> BuildInvoiceEnvelopeAsync(
        ParsedElectronicDocument doc, byte[] xml, string fileName, string source, CancellationToken ct)
    {
        EnsureReceiver(doc.Receiver.IdentificationType, doc.Receiver.Identification);
        ValidateInvoiceAmounts(doc);
        var existing = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == doc.Clave).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        var crc = doc.CurrencyCode.Equals("CRC", StringComparison.OrdinalIgnoreCase)
            ? doc.Totals.TotalDocument : doc.Totals.TotalDocument * doc.ExchangeRate;
        var high = crc > options.HighAmountReviewThresholdCrc;
        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(), Source = source, FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = existing.HasValue ? ReceivedDocumentInboxStatus.Duplicate : ReceivedDocumentInboxStatus.PendingReview,
            DocumentKind = "FacturaElectronica", Clave = doc.Clave,
            IssuerName = doc.Issuer.Name, IssuerIdentification = doc.Issuer.Identification,
            ReceiverName = doc.Receiver.Name, CurrencyCode = doc.CurrencyCode,
            Total = doc.Totals.TotalDocument, TotalCrcEquivalent = crc,
            RequiresHighAmountReview = high,
            Warnings = high ? [$"Monto extraordinariamente alto: equivalente aproximado CRC {crc:N2}. Requiere revisión manual."] : [],
            ExistingElectronicDocumentId = existing, XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private async Task<ReceivedDocumentInboxEnvelope> BuildHaciendaEnvelopeAsync(
        ParsedHaciendaResponse response, byte[] xml, string fileName, string source, CancellationToken ct)
    {
        EnsureReceiver(response.ReceiverIdentificationType, response.ReceiverIdentification);
        if (response.TotalInvoice < 0 || response.TotalTax < 0)
            throw new ReceivedDocumentInboxException("El mensaje de Hacienda contiene montos negativos no permitidos.");
        var existing = await dbContext.HaciendaResponses.AsNoTracking()
            .Where(x => x.Clave == response.Clave).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        var high = response.TotalInvoice > options.HighAmountReviewThresholdCrc;
        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(), Source = source, FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = existing.HasValue ? ReceivedDocumentInboxStatus.Duplicate : ReceivedDocumentInboxStatus.PendingReview,
            DocumentKind = "MensajeHacienda", Clave = response.Clave,
            IssuerName = response.IssuerName, IssuerIdentification = response.IssuerIdentification,
            ReceiverName = response.ReceiverName, CurrencyCode = "CRC",
            Total = response.TotalInvoice, TotalCrcEquivalent = response.TotalInvoice,
            RequiresHighAmountReview = high,
            Warnings = high ? [$"Monto extraordinariamente alto: CRC {response.TotalInvoice:N2}. Requiere revisión manual."] : [],
            ExistingElectronicDocumentId = existing, XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private void ValidateInvoiceAmounts(ParsedElectronicDocument document)
    {
        if (document.ExchangeRate <= 0)
            throw new ReceivedDocumentInboxException("El tipo de cambio debe ser mayor que cero.");
        foreach (var line in document.Lines)
        {
            if (line.Quantity <= 0)
                throw new ReceivedDocumentInboxException($"La línea {line.LineNumber} contiene una cantidad inválida.");
            EnsureNonNegative(line.LineNumber, "PrecioUnitario", line.UnitPrice);
            EnsureNonNegative(line.LineNumber, "MontoTotal", line.GrossAmount);
            EnsureNonNegative(line.LineNumber, "SubTotal", line.Subtotal);
            EnsureNonNegative(line.LineNumber, "BaseImponible", line.TaxableBase);
            EnsureNonNegative(line.LineNumber, "ImpuestoNeto", line.NetTax);
            EnsureNonNegative(line.LineNumber, "MontoTotalLinea", line.TotalLine);
            foreach (var d in line.Discounts) EnsureNonNegative(line.LineNumber, "MontoDescuento", d.Amount);
            foreach (var tax in line.Taxes)
            {
                EnsureNonNegative(line.LineNumber, "Tarifa", tax.Rate);
                EnsureNonNegative(line.LineNumber, "MontoImpuesto", tax.Amount);
            }
        }
        var t = document.Totals;
        decimal[] amounts = [t.TotalTaxedServices, t.TotalExemptServices, t.TotalExoneratedServices,
            t.TotalNonSubjectServices, t.TotalTaxedGoods, t.TotalExemptGoods, t.TotalExoneratedGoods,
            t.TotalNonSubjectGoods, t.TotalTaxed, t.TotalExempt, t.TotalExonerated, t.TotalNonSubject,
            t.TotalSale, t.TotalDiscounts, t.TotalNetSale, t.TotalTax, t.TotalVatReturned,
            t.TotalOtherCharges, t.TotalDocument];
        if (amounts.Any(x => x < 0))
            throw new ReceivedDocumentInboxException("El documento contiene totales negativos no permitidos.");
        EnsureClose(t.TotalNetSale, t.TotalSale - t.TotalDiscounts,
            "TotalVentaNeta no coincide con TotalVenta menos TotalDescuentos.");
        EnsureClose(t.TotalDocument, t.TotalNetSale + t.TotalTax - t.TotalVatReturned + t.TotalOtherCharges,
            "TotalComprobante no coincide con los componentes del resumen de la factura.");
    }

    private void EnsureClose(decimal actual, decimal expected, string message)
    {
        if (Math.Abs(actual - expected) > options.MonetaryTolerance)
            throw new ReceivedDocumentInboxException(message);
    }
    private static void EnsureNonNegative(int number, string field, decimal value)
    {
        if (value < 0)
            throw new ReceivedDocumentInboxException($"La línea {number} contiene {field} negativo.");
    }
    private bool MatchesCompany(string type, string number) =>
        string.Equals(type, company.TaxIdentificationType, StringComparison.Ordinal) &&
        string.Equals(number, company.TaxIdentificationNumber, StringComparison.Ordinal);
    private void EnsureReceiver(string type, string number)
    {
        if (!MatchesCompany(type, number))
            throw new ReceivedDocumentInboxException("El documento no está dirigido al receptor fiscal configurado para Revestik.");
    }
    private async Task SaveAsync(ReceivedDocumentInboxEnvelope item, CancellationToken ct)
    {
        var encrypted = protector.Protect(JsonSerializer.Serialize(item));
        await File.WriteAllTextAsync(GetPath(item.Id), encrypted, ct);
    }
    private async Task<ReceivedDocumentInboxEnvelope> ReadAsync(string path, CancellationToken ct)
    {
        var json = protector.Unprotect(await File.ReadAllTextAsync(path, ct));
        return JsonSerializer.Deserialize<ReceivedDocumentInboxEnvelope>(json)
            ?? throw new ReceivedDocumentInboxException("No fue posible leer un documento de la bandeja segura.");
    }
    private string GetPath(Guid id) => Path.Combine(storagePath, $"{id:N}.inbox");
    private static ReceivedDocumentInboxItemResponse Map(ReceivedDocumentInboxEnvelope item) => new(
        item.Id, item.Source, item.FileName, item.ReceivedAtUtc, item.Status, item.DocumentKind,
        item.Clave, item.IssuerName, item.IssuerIdentification, item.ReceiverName,
        item.CurrencyCode, item.Total, item.TotalCrcEquivalent, item.RequiresHighAmountReview,
        item.Warnings, item.ExistingElectronicDocumentId, item.AcceptedElectronicDocumentId);
}
