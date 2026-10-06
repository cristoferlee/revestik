using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models;
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
            ParsedReceivedElectronicDocument electronicDocument =>
                await ImportDocumentAsync(electronicDocument.Document, xml, importedByUserId, cancellationToken),
            ParsedReceivedHaciendaResponse haciendaResponse =>
                await ImportHaciendaResponseAsync(haciendaResponse.Response, xml, cancellationToken),
            _ => throw new ElectronicDocumentXmlException("Tipo XML recibido no soportado.")
        };
    }

    private async Task<ElectronicDocumentImportResponse> ImportDocumentAsync(
        ParsedElectronicDocument parsed,
        byte[] xml,
        string importedByUserId,
        CancellationToken cancellationToken)
    {
        EnsureReceiver(parsed.Receiver.IdentificationType, parsed.Receiver.Identification);
        await EnsureActiveUserAsync(importedByUserId, cancellationToken);

        var duplicateId = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (duplicateId.HasValue)
        {
            throw new DuplicateElectronicDocumentException(duplicateId.Value);
        }

        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(
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
        {
            document.Supplier = supplier;
        }

        var response = await dbContext.HaciendaResponses
            .SingleOrDefaultAsync(x => x.Clave == parsed.Clave, cancellationToken);
        if (response is not null)
        {
            response.ElectronicDocument = document;
        }

        dbContext.ElectronicDocuments.Add(document);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.ChangeTracker.Clear();
            var existingId = await dbContext.ElectronicDocuments
                .AsNoTracking()
                .Where(x => x.Clave == parsed.Clave)
                .Select(x => (int?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (existingId.HasValue)
            {
                throw new DuplicateElectronicDocumentException(existingId.Value);
            }
            throw;
        }

        return new ElectronicDocumentImportResponse(
            "ElectronicDocument",
            document.Id,
            document.Clave,
            document.DocumentType.ToString(),
            document.Id);
    }

    private async Task<ElectronicDocumentImportResponse> ImportHaciendaResponseAsync(
        ParsedHaciendaResponse parsed,
        byte[] xml,
        CancellationToken cancellationToken)
    {
        EnsureReceiver(parsed.ReceiverIdentificationType, parsed.ReceiverIdentification);

        var existingId = await dbContext.HaciendaResponses
            .AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingId.HasValue)
        {
            throw new DuplicateElectronicDocumentException(existingId.Value);
        }

        var electronicDocumentId = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x => x.Clave == parsed.Clave)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

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
            var duplicate = await dbContext.HaciendaResponses
                .AsNoTracking()
                .Where(x => x.Clave == parsed.Clave)
                .Select(x => (int?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (duplicate.HasValue)
            {
                throw new DuplicateElectronicDocumentException(duplicate.Value);
            }
            throw;
        }

        return new ElectronicDocumentImportResponse(
            "HaciendaResponse",
            response.Id,
            response.Clave,
            null,
            response.ElectronicDocumentId);
    }

    private ElectronicDocument MapDocument(
        ParsedElectronicDocument parsed,
        byte[] xml,
        string importedByUserId)
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
        {
            throw new InvalidElectronicDocumentReceiverException();
        }
    }

    private async Task EnsureActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        var active = !string.IsNullOrWhiteSpace(userId) &&
            await dbContext.Users.AsNoTracking().AnyAsync(
                x => x.Id == userId && x.IsActive,
                cancellationToken);

        if (!active)
        {
            throw new InvalidOperationException("The authenticated user does not exist or is inactive.");
        }
    }
}
