using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ElectronicDocumentImportService(
    RevestikDbContext dbContext,
    IElectronicDocumentXmlParser parser,
    IOptions<CompanyOptions> companyOptions)
    : IElectronicDocumentImportService
{
    public async Task<ElectronicDocumentImportResponse> ImportAsync(
        byte[] xml,
        string importedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var parsed = parser.Parse(xml);
        return parsed switch
        {
            ParsedReceivedElectronicDocument document =>
                await ImportDocumentAsync(document.Document, xml, importedByUserId, cancellationToken),
            ParsedReceivedHaciendaResponse response =>
                await ImportHaciendaResponseAsync(response.Response, xml, cancellationToken),
            ParsedHaciendaDocument note when note.Kind is HaciendaXmlDocumentKind.NotaCreditoElectronica
                or HaciendaXmlDocumentKind.NotaDebitoElectronica =>
                await ImportNoteAsync(note, xml, importedByUserId, cancellationToken),
            ParsedHaciendaDocument ticket when ticket.Kind == HaciendaXmlDocumentKind.TiqueteElectronico =>
                await ImportTicketAsync(ticket, xml, importedByUserId, cancellationToken),
            ParsedHaciendaDocument other when other.Kind is
                HaciendaXmlDocumentKind.FacturaElectronicaCompra or
                HaciendaXmlDocumentKind.FacturaElectronicaExportacion or
                HaciendaXmlDocumentKind.ReciboElectronicoPago =>
                await ImportOtherFiscalDocumentAsync(other, xml, importedByUserId, cancellationToken),
            _ => throw new ElectronicDocumentXmlException("Tipo XML recibido no soportado.")
        };
    }

    private async Task<ElectronicDocumentImportResponse> ImportOtherFiscalDocumentAsync(
        ParsedHaciendaDocument fiscal, byte[] xml, string importedByUserId,
        CancellationToken cancellationToken)
    {
        if (fiscal.FiscalTotals is null || fiscal.FiscalLines is null ||
            fiscal.FiscalTotals.TotalDocument != fiscal.TotalDocument)
            throw new ElectronicDocumentXmlException(
                "El documento no contiene un desglose fiscal completo.");

        if (fiscal.Clave.Length != 50 || !fiscal.Clave.All(char.IsAsciiDigit))
            throw new ElectronicDocumentXmlException("La clave fiscal es inválida.");

        var company = companyOptions.Value;
        var issued = MatchesCompany(fiscal.Issuer.IdentificationType,
            fiscal.Issuer.Identification, company);
        var received = fiscal.Receiver is not null && MatchesCompany(
            fiscal.Receiver.IdentificationType, fiscal.Receiver.Identification, company);
        if (issued == received)
            throw new InvalidElectronicDocumentReceiverException();
        if (fiscal.Receiver is null && !issued)
            throw new InvalidElectronicDocumentReceiverException();
        if (string.IsNullOrWhiteSpace(fiscal.CurrencyCode) ||
            (fiscal.ExchangeRate.HasValue && fiscal.ExchangeRate <= 0m) ||
            (!fiscal.CurrencyCode.Equals("CRC", StringComparison.OrdinalIgnoreCase) &&
             fiscal.ExchangeRate is null))
            throw new ElectronicDocumentXmlException("Moneda o tipo de cambio inválido.");

        await EnsureActiveUserAsync(importedByUserId, cancellationToken);
        var duplicateId = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == fiscal.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (duplicateId.HasValue)
            throw new DuplicateElectronicDocumentException(duplicateId.Value);

        var documentType = fiscal.Kind switch
        {
            HaciendaXmlDocumentKind.FacturaElectronicaCompra => ElectronicDocumentType.PurchaseInvoice,
            HaciendaXmlDocumentKind.FacturaElectronicaExportacion => ElectronicDocumentType.ExportInvoice,
            HaciendaXmlDocumentKind.ReciboElectronicoPago => ElectronicDocumentType.ElectronicPaymentReceipt,
            _ => throw new ElectronicDocumentXmlException("Tipo fiscal no soportado.")
        };
        var issuer = fiscal.Issuer;
        var receiver = fiscal.Receiver;
        var parsed = new ParsedElectronicDocument(
            documentType, fiscal.Clave, fiscal.NumeroConsecutivo, fiscal.FechaEmision,
            fiscal.IssuerEconomicActivityCode, fiscal.ReceiverEconomicActivityCode,
            new ParsedParty(issuer.Name, issuer.CommercialName, issuer.IdentificationType,
                issuer.Identification, string.Empty, issuer.Email, string.Empty),
            receiver is null
                ? new ParsedParty(string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty)
                : new ParsedParty(receiver.Name, receiver.CommercialName,
                    receiver.IdentificationType, receiver.Identification,
                    string.Empty, receiver.Email, string.Empty),
            fiscal.SaleConditionCode, fiscal.CreditTermDays,
            fiscal.CurrencyCode, fiscal.ExchangeRate ?? 1m,
            fiscal.FiscalTotals, fiscal.FiscalLines);

        var document = MapDocument(parsed, xml, importedByUserId);
        document.Direction = issued ? ElectronicDocumentDirection.Issued
            : ElectronicDocumentDirection.Received;
        if (fiscal.Kind == HaciendaXmlDocumentKind.ReciboElectronicoPago)
            document.References = MapFiscalReferences(fiscal.References);
        // Fiscal evidence only. No supplier, purchase, payment, inventory or accounting effect.
        // The receipt is not a second expense; all these types remain unclassified.
        dbContext.ElectronicDocuments.Add(document);
        await SaveDocumentWithDuplicateProtectionAsync(document, cancellationToken);
        return new ElectronicDocumentImportResponse(
            "ElectronicDocument", document.Id, document.Clave,
            document.DocumentType.ToString(), document.Id);
    }

    private async Task<ElectronicDocumentImportResponse> ImportTicketAsync(
        ParsedHaciendaDocument ticket, byte[] xml, string importedByUserId,
        CancellationToken cancellationToken)
    {
        if (ticket.FiscalTotals is null || ticket.FiscalLines is null ||
            ticket.FiscalTotals.TotalDocument != ticket.TotalDocument)
            throw new ElectronicDocumentXmlException("El tiquete no contiene un desglose fiscal completo.");

        if (ticket.Clave.Length != 50 || !ticket.Clave.All(char.IsAsciiDigit))
            throw new ElectronicDocumentXmlException("El tiquete contiene una clave fiscal inválida.");

        var company = companyOptions.Value;
        var issued = MatchesCompany(ticket.Issuer.IdentificationType, ticket.Issuer.Identification, company);
        var received = ticket.Receiver is not null &&
            MatchesCompany(ticket.Receiver.IdentificationType, ticket.Receiver.Identification, company);

        if (ticket.Receiver is not null && !issued && !received)
            throw new InvalidElectronicDocumentReceiverException();
        if (issued && received)
            throw new ElectronicDocumentXmlException("No es posible determinar la dirección fiscal del tiquete.");

        if (string.IsNullOrWhiteSpace(ticket.CurrencyCode) ||
            ticket.ExchangeRate is <= 0 ||
            (!string.Equals(ticket.CurrencyCode, "CRC", StringComparison.OrdinalIgnoreCase) &&
             ticket.ExchangeRate is null))
            throw new ElectronicDocumentXmlException("El tiquete no contiene una moneda o tipo de cambio válido.");

        await EnsureActiveUserAsync(importedByUserId, cancellationToken);
        var duplicateId = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == ticket.Clave).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (duplicateId.HasValue)
            throw new DuplicateElectronicDocumentException(duplicateId.Value);

        var issuer = ticket.Issuer;
        var receiver = ticket.Receiver;
        var parsed = new ParsedElectronicDocument(
            ElectronicDocumentType.ElectronicTicket,
            ticket.Clave, ticket.NumeroConsecutivo, ticket.FechaEmision,
            ticket.IssuerEconomicActivityCode, ticket.ReceiverEconomicActivityCode,
            new ParsedParty(issuer.Name, issuer.CommercialName, issuer.IdentificationType,
                issuer.Identification, string.Empty, issuer.Email, string.Empty),
            receiver is null
                ? new ParsedParty(string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty)
                : new ParsedParty(receiver.Name, receiver.CommercialName,
                    receiver.IdentificationType, receiver.Identification,
                    string.Empty, receiver.Email, string.Empty),
            ticket.SaleConditionCode, ticket.CreditTermDays,
            ticket.CurrencyCode, ticket.ExchangeRate ?? 1m,
            ticket.FiscalTotals, ticket.FiscalLines);

        var document = MapDocument(parsed, xml, importedByUserId);
        document.Direction = issued ? ElectronicDocumentDirection.Issued : ElectronicDocumentDirection.Received;
        // A ticket without a recipient is kept only as an unclassified fiscal record.
        // It is not evidence of deductibility or an authorization to create an expense.
        dbContext.ElectronicDocuments.Add(document);
        await SaveDocumentWithDuplicateProtectionAsync(document, cancellationToken);
        return new ElectronicDocumentImportResponse(
            "ElectronicDocument", document.Id, document.Clave,
            document.DocumentType.ToString(), document.Id);
    }

    private async Task<ElectronicDocumentImportResponse> ImportNoteAsync(
        ParsedHaciendaDocument note, byte[] xml, string importedByUserId,
        CancellationToken cancellationToken)
    {
        if (note.FiscalTotals is null || note.FiscalLines is null ||
            note.FiscalTotals.TotalDocument != note.TotalDocument)
            throw new ElectronicDocumentXmlException("La nota no contiene un desglose fiscal completo.");

        var company = companyOptions.Value;
        bool issuerMatches = MatchesCompany(note.Issuer.IdentificationType, note.Issuer.Identification, company);
        bool receiverMatches = note.Receiver is not null &&
            MatchesCompany(note.Receiver.IdentificationType, note.Receiver.Identification, company);

        // Never infer that an unrelated note belongs to Revestik.
        if (!issuerMatches && !receiverMatches)
            throw new InvalidElectronicDocumentReceiverException();
        if (note.Receiver is not null && !issuerMatches && !receiverMatches)
            throw new InvalidElectronicDocumentReceiverException();
        if (issuerMatches && receiverMatches)
            throw new ElectronicDocumentXmlException("No es posible determinar la dirección fiscal de la nota.");

        var direction = issuerMatches
            ? ElectronicDocumentDirection.Issued
            : ElectronicDocumentDirection.Received;

        if (note.Receiver is null && direction != ElectronicDocumentDirection.Issued)
            throw new InvalidElectronicDocumentReceiverException();

        if (string.IsNullOrWhiteSpace(note.Clave) || note.Clave.Length != 50 ||
            !note.Clave.All(char.IsAsciiDigit))
            throw new ElectronicDocumentXmlException("La nota contiene una clave fiscal inválida.");
        if (string.IsNullOrWhiteSpace(note.CurrencyCode))
            throw new ElectronicDocumentXmlException("La nota no indica una moneda fiscal.");
        if (note.CurrencyCode != "CRC" && (!note.ExchangeRate.HasValue || note.ExchangeRate <= 0))
            throw new ElectronicDocumentXmlException("La nota requiere un tipo de cambio válido.");
        if (note.ExchangeRate.HasValue && note.ExchangeRate <= 0)
            throw new ElectronicDocumentXmlException("El tipo de cambio debe ser positivo.");

        await EnsureActiveUserAsync(importedByUserId, cancellationToken);
        var existingId = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == note.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
            throw new DuplicateElectronicDocumentException(existingId.Value);

        var parsed = new ParsedElectronicDocument(
            note.Kind == HaciendaXmlDocumentKind.NotaCreditoElectronica
                ? ElectronicDocumentType.CreditNote : ElectronicDocumentType.DebitNote,
            note.Clave, note.NumeroConsecutivo, note.FechaEmision,
            note.IssuerEconomicActivityCode, note.ReceiverEconomicActivityCode,
            new ParsedParty(note.Issuer.Name, note.Issuer.CommercialName,
                note.Issuer.IdentificationType, note.Issuer.Identification,
                string.Empty, note.Issuer.Email, string.Empty),
            note.Receiver is null
                ? new ParsedParty(string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty)
                : new ParsedParty(note.Receiver.Name, note.Receiver.CommercialName,
                    note.Receiver.IdentificationType, note.Receiver.Identification,
                    string.Empty, note.Receiver.Email, string.Empty),
            note.SaleConditionCode, note.CreditTermDays,
            note.CurrencyCode, note.ExchangeRate ?? 1m,
            note.FiscalTotals, note.FiscalLines);

        var references = MapFiscalReferences(note.References);
        var document = MapDocument(parsed, xml, importedByUserId);
        document.Direction = direction;
        document.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        document.References = references;
        // References remain unlinked pending a separate fiscal review (4.3).
        // No supplier, purchase, payment, category, or financial posting is created.
        dbContext.ElectronicDocuments.Add(document);

        await SaveDocumentWithDuplicateProtectionAsync(document, cancellationToken);
        return new ElectronicDocumentImportResponse(
            "ElectronicDocument", document.Id, document.Clave,
            document.DocumentType.ToString(), document.Id);
    }

    private static List<ElectronicDocumentReference> MapFiscalReferences(
        IReadOnlyList<ParsedHaciendaReference> references)
    {
        // Notes in Hacienda 4.4 have from one to ten reference entries.
        if (references.Count is < 1 or > 10)
            throw new ElectronicDocumentXmlException(
                "La nota debe contener entre una y diez referencias fiscales.");

        var result = new List<ElectronicDocumentReference>(references.Count);
        for (var index = 0; index < references.Count; index++)
        {
            var item = references[index];
            if (string.IsNullOrWhiteSpace(item.DocumentType) || item.DocumentType.Length > 2 ||
                string.IsNullOrWhiteSpace(item.Number) || item.Number.Length > 50 ||
                item.Code.Length > 2 || item.Reason.Length > 500)
                throw new ElectronicDocumentXmlException(
                    $"La referencia fiscal {index + 1} contiene datos inválidos.");

            result.Add(new ElectronicDocumentReference
            {
                Sequence = index + 1,
                ReferencedDocumentTypeCode = item.DocumentType,
                ReferenceNumber = item.Number,
                ReferencedIssueDate = item.IssueDate,
                ReferenceCode = item.Code,
                Reason = item.Reason,
                RelatedElectronicDocumentId = null
            });
        }

        return result;
    }

    private static bool MatchesCompany(string type, string number, CompanyOptions company) =>
        !string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(number) &&
        string.Equals(type, company.TaxIdentificationType, StringComparison.Ordinal) &&
        string.Equals(number, company.TaxIdentificationNumber, StringComparison.Ordinal);

    private async Task<ElectronicDocumentImportResponse> ImportDocumentAsync(
        ParsedElectronicDocument parsed, byte[] xml,
        string importedByUserId, CancellationToken cancellationToken)
    {
        EnsureReceiver(parsed.Receiver.IdentificationType, parsed.Receiver.Identification);
        await EnsureActiveUserAsync(importedByUserId, cancellationToken);

        var duplicateId = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (duplicateId.HasValue)
            throw new DuplicateElectronicDocumentException(duplicateId.Value);

        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(
            x => x.IdentificationType == parsed.Issuer.IdentificationType &&
                 x.IdentificationNumber == parsed.Issuer.Identification,
            cancellationToken);

        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = parsed.Issuer.Name,
                ContactName = string.Empty,
                PhoneNumber = parsed.Issuer.PhoneNumber,
                Email = parsed.Issuer.Email,
                IdentificationType = parsed.Issuer.IdentificationType,
                IdentificationNumber = parsed.Issuer.Identification,
                CommercialName = parsed.Issuer.CommercialName,
                Address = parsed.Issuer.Address,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.Suppliers.Add(supplier);
        }

        var document = MapDocument(parsed, xml, importedByUserId);
        if (supplier.Id == 0)
            document.Supplier = supplier;

        var response = await dbContext.HaciendaResponses
            .SingleOrDefaultAsync(x => x.Clave == parsed.Clave, cancellationToken);
        if (response is not null)
            response.ElectronicDocument = document;

        dbContext.ElectronicDocuments.Add(document);
        await SaveDocumentWithDuplicateProtectionAsync(document, cancellationToken);
        return new ElectronicDocumentImportResponse(
            "ElectronicDocument", document.Id, document.Clave,
            document.DocumentType.ToString(), document.Id);
    }

    private async Task SaveDocumentWithDuplicateProtectionAsync(
        ElectronicDocument document, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.ChangeTracker.Clear();
            var existingId = await dbContext.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Clave == document.Clave)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
            if (existingId.HasValue)
                throw new DuplicateElectronicDocumentException(existingId.Value);
            throw;
        }
    }

    private async Task<ElectronicDocumentImportResponse> ImportHaciendaResponseAsync(
        ParsedHaciendaResponse parsed, byte[] xml,
        CancellationToken cancellationToken)
    {
        EnsureReceiver(parsed.ReceiverIdentificationType, parsed.ReceiverIdentification);
        var existingId = await dbContext.HaciendaResponses.AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
            throw new DuplicateElectronicDocumentException(existingId.Value);

        var electronicDocumentId = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);

        var response = new HaciendaResponse
        {
            Clave = parsed.Clave,
            IssuerName = parsed.IssuerName,
            IssuerIdentificationType = parsed.IssuerIdentificationType,
            IssuerIdentification = parsed.IssuerIdentification,
            ReceiverName = parsed.ReceiverName,
            ReceiverIdentificationType = parsed.ReceiverIdentificationType,
            ReceiverIdentification = parsed.ReceiverIdentification,
            MessageCode = parsed.MessageCode,
            MessageStatus = parsed.MessageStatus,
            MessageDetail = parsed.MessageDetail,
            TotalTax = parsed.TotalTax,
            TotalInvoice = parsed.TotalInvoice,
            OriginalXml = xml,
            ReceivedAtUtc = DateTime.UtcNow,
            ElectronicDocumentId = electronicDocumentId
        };
        dbContext.HaciendaResponses.Add(response);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.ChangeTracker.Clear();
            var duplicate = await dbContext.HaciendaResponses.AsNoTracking()
                .Where(x => x.Clave == parsed.Clave)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
            if (duplicate.HasValue)
                throw new DuplicateElectronicDocumentException(duplicate.Value);
            throw;
        }
        return new ElectronicDocumentImportResponse(
            "HaciendaResponse", response.Id, response.Clave,
            null, response.ElectronicDocumentId);
    }

    private ElectronicDocument MapDocument(
        ParsedElectronicDocument parsed, byte[] xml, string importedByUserId)
    {
        var totals = parsed.Totals;
        return new ElectronicDocument
        {
            Clave = parsed.Clave,
            DocumentType = parsed.DocumentType,
            NumeroConsecutivo = parsed.NumeroConsecutivo,
            FechaEmision = parsed.FechaEmision,
            IssuerEconomicActivityCode = parsed.IssuerEconomicActivityCode,
            ReceiverEconomicActivityCode = parsed.ReceiverEconomicActivityCode,
            IssuerName = parsed.Issuer.Name,
            IssuerCommercialName = parsed.Issuer.CommercialName,
            IssuerIdentificationType = parsed.Issuer.IdentificationType,
            IssuerIdentification = parsed.Issuer.Identification,
            IssuerPhoneNumber = parsed.Issuer.PhoneNumber,
            IssuerEmail = parsed.Issuer.Email,
            IssuerAddress = parsed.Issuer.Address,
            ReceiverName = parsed.Receiver.Name,
            ReceiverIdentificationType = parsed.Receiver.IdentificationType,
            ReceiverIdentification = parsed.Receiver.Identification,
            SaleConditionCode = parsed.SaleConditionCode,
            CreditTermDays = parsed.CreditTermDays,
            CurrencyCode = parsed.CurrencyCode,
            ExchangeRate = parsed.ExchangeRate,
            TotalTaxedServices = totals.TotalTaxedServices,
            TotalExemptServices = totals.TotalExemptServices,
            TotalExoneratedServices = totals.TotalExoneratedServices,
            TotalNonSubjectServices = totals.TotalNonSubjectServices,
            TotalTaxedGoods = totals.TotalTaxedGoods,
            TotalExemptGoods = totals.TotalExemptGoods,
            TotalExoneratedGoods = totals.TotalExoneratedGoods,
            TotalNonSubjectGoods = totals.TotalNonSubjectGoods,
            TotalTaxed = totals.TotalTaxed,
            TotalExempt = totals.TotalExempt,
            TotalExonerated = totals.TotalExonerated,
            TotalNonSubject = totals.TotalNonSubject,
            TotalSale = totals.TotalSale,
            TotalDiscounts = totals.TotalDiscounts,
            TotalNetSale = totals.TotalNetSale,
            TotalTax = totals.TotalTax,
            TotalVatReturned = totals.TotalVatReturned,
            TotalOtherCharges = totals.TotalOtherCharges,
            TotalDocument = totals.TotalDocument,
            ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
            OriginalXml = xml,
            ImportedByUserId = importedByUserId,
            ImportedAtUtc = DateTime.UtcNow,
            Lines = parsed.Lines.Select(line => new ElectronicDocumentLine
            {
                LineNumber = line.LineNumber,
                CabysCode = line.CabysCode,
                CommercialCodeType = line.CommercialCodeType,
                CommercialCode = line.CommercialCode,
                Quantity = line.Quantity,
                UnitOfMeasure = line.UnitOfMeasure,
                CommercialUnitOfMeasure = line.CommercialUnitOfMeasure,
                Description = line.Description,
                UnitPrice = line.UnitPrice,
                GrossAmount = line.GrossAmount,
                Subtotal = line.Subtotal,
                TaxableBase = line.TaxableBase,
                NetTax = line.NetTax,
                TotalLine = line.TotalLine,
                Discounts = line.Discounts.Select(discount => new ElectronicDocumentLineDiscount
                {
                    Amount = discount.Amount,
                    Code = discount.Code,
                    Nature = discount.Nature
                }).ToList(),
                Taxes = line.Taxes.Select(tax => new ElectronicDocumentLineTax
                {
                    TaxCode = tax.TaxCode,
                    VatRateCode = tax.VatRateCode,
                    Rate = tax.Rate,
                    Amount = tax.Amount
                }).ToList()
            }).ToList()
        };
    }

    private void EnsureReceiver(string identificationType, string identification)
    {
        var company = companyOptions.Value;
        if (!string.Equals(identificationType, company.TaxIdentificationType, StringComparison.Ordinal) ||
            !string.Equals(identification, company.TaxIdentificationNumber, StringComparison.Ordinal))
            throw new InvalidElectronicDocumentReceiverException();
    }

    private async Task EnsureActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        var active = !string.IsNullOrWhiteSpace(userId) &&
            await dbContext.Users.AsNoTracking().AnyAsync(
                x => x.Id == userId && x.IsActive, cancellationToken);
        if (!active)
            throw new InvalidOperationException("The authenticated user does not exist or is inactive.");
    }
}
