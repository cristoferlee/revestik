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
        byte[] xml,
        string fileName,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(xml);

        if (xml.Length == 0)
            throw new ReceivedDocumentInboxException("El archivo XML está vacío.");

        if (xml.Length > MaxXmlBytes)
            throw new ReceivedDocumentInboxException("El archivo XML no puede superar 5 MB.");

        if (!string.Equals(Path.GetExtension(fileName), ".xml", StringComparison.OrdinalIgnoreCase))
            throw new ReceivedDocumentInboxException("Solo se permiten archivos con extensión .xml.");

        HaciendaXmlValidationResult schemaValidation;
        try
        {
            schemaValidation = schemaValidator.Validate(xml);
        }
        catch (HaciendaXmlValidationException exception)
        {
            throw new ReceivedDocumentInboxException(exception.Message);
        }

        ReceivedDocumentInboxEnvelope envelope;
        if (!schemaValidation.Descriptor.ProcessingEnabled)
        {
            envelope = BuildRecognizedNotEnabledEnvelope(
                schemaValidation.Descriptor,
                xml,
                fileName,
                source);
        }
        else
        {
            ParsedReceivedXml parsed;
            try
            {
                parsed = parser.Parse(xml);
            }
            catch (ElectronicDocumentXmlException exception)
            {
                throw new ReceivedDocumentInboxException(exception.Message);
            }

            envelope = parsed switch
            {
                ParsedReceivedElectronicDocument received =>
                    await BuildInvoiceEnvelopeAsync(received.Document, xml, fileName, source, cancellationToken),
                ParsedReceivedHaciendaResponse response =>
                    await BuildHaciendaEnvelopeAsync(response.Response, xml, fileName, source, cancellationToken),
                _ => throw new ReceivedDocumentInboxException("Tipo XML recibido no soportado.")
            };
        }

        await SaveAsync(envelope, cancellationToken);
        return Map(envelope);
    }

    public async Task<IReadOnlyList<ReceivedDocumentInboxItemResponse>> GetPendingAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<ReceivedDocumentInboxItemResponse>();

        foreach (var path in Directory.EnumerateFiles(storagePath, "*.inbox"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var envelope = await ReadAsync(path, cancellationToken);

            if (envelope.Status is ReceivedDocumentInboxStatus.PendingReview
                or ReceivedDocumentInboxStatus.Duplicate
                or ReceivedDocumentInboxStatus.RecognizedNotEnabled)
            {
                items.Add(Map(envelope));
            }
        }

        return items.OrderByDescending(x => x.ReceivedAtUtc).ToList();
    }

    public async Task<ReceivedDocumentInboxAcceptResponse> AcceptAsync(
        Guid inboxId,
        string userId,
        CancellationToken cancellationToken)
    {
        var path = GetPath(inboxId);
        if (!File.Exists(path))
            throw new KeyNotFoundException("El documento pendiente no existe.");

        var envelope = await ReadAsync(path, cancellationToken);

        if (envelope.Status == ReceivedDocumentInboxStatus.Duplicate)
            throw new ReceivedDocumentInboxException("El documento es duplicado y no puede aceptarse nuevamente.");

        if (envelope.Status == ReceivedDocumentInboxStatus.RecognizedNotEnabled)
            throw new ReceivedDocumentInboxException("El tipo de documento Hacienda es válido, pero su procesamiento todavía no está habilitado en Revestik.");

        if (envelope.Status != ReceivedDocumentInboxStatus.PendingReview)
            throw new ReceivedDocumentInboxException("El documento ya no está pendiente de revisión.");

        var xml = Convert.FromBase64String(envelope.XmlBase64);

        try
        {
            var imported = await importService.ImportAsync(xml, userId, cancellationToken);

            envelope.Status = ReceivedDocumentInboxStatus.Accepted;
            envelope.AcceptedElectronicDocumentId = imported.ElectronicDocumentId ?? imported.Id;
            envelope.XmlBase64 = string.Empty; // raw staging payload is destroyed after canonical import
            await SaveAsync(envelope, cancellationToken);

            return new ReceivedDocumentInboxAcceptResponse(envelope.Id, imported);
        }
        catch (DuplicateElectronicDocumentException exception)
        {
            envelope.Status = ReceivedDocumentInboxStatus.Duplicate;
            envelope.ExistingElectronicDocumentId = exception.ExistingId;
            await SaveAsync(envelope, cancellationToken);
            throw new ReceivedDocumentInboxException("El documento ya existe en Revestik.");
        }
    }

    public async Task<bool> RejectAsync(
        Guid inboxId,
        CancellationToken cancellationToken)
    {
        var path = GetPath(inboxId);
        if (!File.Exists(path))
            return false;

        var envelope = await ReadAsync(path, cancellationToken);
        envelope.Status = ReceivedDocumentInboxStatus.Rejected;
        envelope.XmlBase64 = string.Empty; // do not retain rejected untrusted XML
        await SaveAsync(envelope, cancellationToken);
        return true;
    }


    private static ReceivedDocumentInboxEnvelope BuildRecognizedNotEnabledEnvelope(
        HaciendaXmlDocumentDescriptor descriptor,
        byte[] xml,
        string fileName,
        string source)
    {
        var preview = HaciendaXmlPreviewReader.Read(xml);

        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(),
            Source = source,
            FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = ReceivedDocumentInboxStatus.RecognizedNotEnabled,
            DocumentKind = descriptor.RootElement,
            Clave = preview.Clave,
            IssuerName = preview.IssuerName,
            IssuerIdentification = preview.IssuerIdentification,
            ReceiverName = preview.ReceiverName,
            CurrencyCode = preview.CurrencyCode,
            Total = preview.Total,
            TotalCrcEquivalent = preview.Total,
            RequiresHighAmountReview = false,
            Warnings =
            [
                "El XML cumple una estructura Hacienda v4.4 reconocida, pero este tipo de documento todavía no tiene reglas de negocio habilitadas en Revestik."
            ],
            XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private async Task<ReceivedDocumentInboxEnvelope> BuildInvoiceEnvelopeAsync(
        ParsedElectronicDocument document,
        byte[] xml,
        string fileName,
        string source,
        CancellationToken cancellationToken)
    {
        EnsureReceiver(document.Receiver.IdentificationType, document.Receiver.Identification);
        ValidateInvoiceAmounts(document);

        var existingId = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x => x.Clave == document.Clave)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var crcEquivalent = document.CurrencyCode.Equals("CRC", StringComparison.OrdinalIgnoreCase)
            ? document.Totals.TotalDocument
            : document.Totals.TotalDocument * document.ExchangeRate;

        var highAmount = crcEquivalent > options.HighAmountReviewThresholdCrc;
        var warnings = highAmount
            ? new List<string>
            {
                $"Monto extraordinariamente alto: equivalente aproximado CRC {crcEquivalent:N2}. Requiere revisión manual."
            }
            : [];

        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(),
            Source = source,
            FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = existingId.HasValue
                ? ReceivedDocumentInboxStatus.Duplicate
                : ReceivedDocumentInboxStatus.PendingReview,
            DocumentKind = "FacturaElectronica",
            Clave = document.Clave,
            IssuerName = document.Issuer.Name,
            IssuerIdentification = document.Issuer.Identification,
            ReceiverName = document.Receiver.Name,
            CurrencyCode = document.CurrencyCode,
            Total = document.Totals.TotalDocument,
            TotalCrcEquivalent = crcEquivalent,
            RequiresHighAmountReview = highAmount,
            Warnings = warnings,
            ExistingElectronicDocumentId = existingId,
            XmlBase64 = Convert.ToBase64String(xml)
        };
    }

    private async Task<ReceivedDocumentInboxEnvelope> BuildHaciendaEnvelopeAsync(
        ParsedHaciendaResponse response,
        byte[] xml,
        string fileName,
        string source,
        CancellationToken cancellationToken)
    {
        EnsureReceiver(response.ReceiverIdentificationType, response.ReceiverIdentification);

        if (response.TotalInvoice < 0 || response.TotalTax < 0)
            throw new ReceivedDocumentInboxException("El mensaje de Hacienda contiene montos negativos no permitidos.");

        var existingId = await dbContext.HaciendaResponses
            .AsNoTracking()
            .Where(x => x.Clave == response.Clave)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var highAmount = response.TotalInvoice > options.HighAmountReviewThresholdCrc;

        return new ReceivedDocumentInboxEnvelope
        {
            Id = Guid.NewGuid(),
            Source = source,
            FileName = Path.GetFileName(fileName),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = existingId.HasValue
                ? ReceivedDocumentInboxStatus.Duplicate
                : ReceivedDocumentInboxStatus.PendingReview,
            DocumentKind = "MensajeHacienda",
            Clave = response.Clave,
            IssuerName = response.IssuerName,
            IssuerIdentification = response.IssuerIdentification,
            ReceiverName = response.ReceiverName,
            CurrencyCode = "CRC",
            Total = response.TotalInvoice,
            TotalCrcEquivalent = response.TotalInvoice,
            RequiresHighAmountReview = highAmount,
            Warnings = highAmount
                ? [$"Monto extraordinariamente alto: CRC {response.TotalInvoice:N2}. Requiere revisión manual."]
                : [],
            ExistingElectronicDocumentId = existingId,
            XmlBase64 = Convert.ToBase64String(xml)
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

            foreach (var discount in line.Discounts)
                EnsureNonNegative(line.LineNumber, "MontoDescuento", discount.Amount);

            foreach (var tax in line.Taxes)
            {
                EnsureNonNegative(line.LineNumber, "Tarifa", tax.Rate);
                EnsureNonNegative(line.LineNumber, "MontoImpuesto", tax.Amount);
            }
        }

        var totals = document.Totals;
        var monetaryValues = new[]
        {
            totals.TotalTaxedServices, totals.TotalExemptServices,
            totals.TotalExoneratedServices, totals.TotalNonSubjectServices,
            totals.TotalTaxedGoods, totals.TotalExemptGoods,
            totals.TotalExoneratedGoods, totals.TotalNonSubjectGoods,
            totals.TotalTaxed, totals.TotalExempt, totals.TotalExonerated,
            totals.TotalNonSubject, totals.TotalSale, totals.TotalDiscounts,
            totals.TotalNetSale, totals.TotalTax, totals.TotalVatReturned,
            totals.TotalOtherCharges, totals.TotalDocument
        };

        if (monetaryValues.Any(x => x < 0))
            throw new ReceivedDocumentInboxException("El documento contiene totales negativos no permitidos.");

        EnsureClose(
            totals.TotalNetSale,
            totals.TotalSale - totals.TotalDiscounts,
            "TotalVentaNeta no coincide con TotalVenta menos TotalDescuentos.");

        EnsureClose(
            totals.TotalDocument,
            totals.TotalNetSale + totals.TotalTax - totals.TotalVatReturned + totals.TotalOtherCharges,
            "TotalComprobante no coincide con los componentes del resumen de la factura.");
    }

    private void EnsureClose(decimal actual, decimal expected, string message)
    {
        if (Math.Abs(actual - expected) > options.MonetaryTolerance)
            throw new ReceivedDocumentInboxException(message);
    }

    private static void EnsureNonNegative(int lineNumber, string field, decimal value)
    {
        if (value < 0)
            throw new ReceivedDocumentInboxException(
                $"La línea {lineNumber} contiene {field} negativo.");
    }

    private void EnsureReceiver(string identificationType, string identification)
    {
        if (!string.Equals(identificationType, company.TaxIdentificationType, StringComparison.Ordinal) ||
            !string.Equals(identification, company.TaxIdentificationNumber, StringComparison.Ordinal))
        {
            throw new ReceivedDocumentInboxException(
                "El documento no está dirigido al receptor fiscal configurado para Revestik.");
        }
    }

    private async Task SaveAsync(
        ReceivedDocumentInboxEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope);
        var encrypted = protector.Protect(json);
        await File.WriteAllTextAsync(GetPath(envelope.Id), encrypted, cancellationToken);
    }

    private async Task<ReceivedDocumentInboxEnvelope> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var encrypted = await File.ReadAllTextAsync(path, cancellationToken);
        var json = protector.Unprotect(encrypted);

        return JsonSerializer.Deserialize<ReceivedDocumentInboxEnvelope>(json)
            ?? throw new ReceivedDocumentInboxException("No fue posible leer un documento de la bandeja segura.");
    }

    private string GetPath(Guid id) => Path.Combine(storagePath, $"{id:N}.inbox");

    private static ReceivedDocumentInboxItemResponse Map(ReceivedDocumentInboxEnvelope item) =>
        new(
            item.Id,
            item.Source,
            item.FileName,
            item.ReceivedAtUtc,
            item.Status,
            item.DocumentKind,
            item.Clave,
            item.IssuerName,
            item.IssuerIdentification,
            item.ReceiverName,
            item.CurrencyCode,
            item.Total,
            item.TotalCrcEquivalent,
            item.RequiresHighAmountReview,
            item.Warnings,
            item.ExistingElectronicDocumentId,
            item.AcceptedElectronicDocumentId);
}
