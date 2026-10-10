using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Read-only comparison of fiscal evidence and an operational purchase.
/// Warnings are informational: purchase lines do not store full tax/discount breakdowns.
/// </summary>
public sealed class ElectronicDocumentPurchaseReconciliationService(RevestikDbContext dbContext)
{
    public async Task<PurchaseReconciliationResult> ReviewAsync(
        int electronicDocumentId, int purchaseId, CancellationToken cancellationToken = default)
    {
        if (electronicDocumentId <= 0 || purchaseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(electronicDocumentId));

        var document = await dbContext.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == electronicDocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("El documento fiscal no existe.");
        var purchase = await dbContext.Purchases.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == purchaseId, cancellationToken)
            ?? throw new KeyNotFoundException("La compra no existe.");

        var warnings = new List<PurchaseReconciliationWarning>();
        var supported = document.Direction == ElectronicDocumentDirection.Received &&
            document.DocumentType is ElectronicDocumentType.Invoice or
                ElectronicDocumentType.ElectronicTicket or
                ElectronicDocumentType.PurchaseInvoice or
                ElectronicDocumentType.ExportInvoice;
        if (!supported)
            warnings.Add(new("UnsupportedDocument", "Este documento no puede vincularse directamente con una compra."));

        var sameSupplier = !string.IsNullOrWhiteSpace(document.IssuerIdentification) &&
            string.Equals(document.IssuerIdentificationType, purchase.Supplier.IdentificationType, StringComparison.Ordinal) &&
            string.Equals(document.IssuerIdentification, purchase.Supplier.IdentificationNumber, StringComparison.Ordinal);
        if (!sameSupplier)
            warnings.Add(new("SupplierMismatch", "El emisor fiscal no coincide con el proveedor de la compra."));

        var purchaseCurrency = purchase.Currency == PurchaseCurrency.CRC ? "CRC" : "USD";
        var sameCurrency = string.Equals(document.CurrencyCode, purchaseCurrency, StringComparison.OrdinalIgnoreCase);
        if (!sameCurrency)
            warnings.Add(new("CurrencyMismatch", "Las monedas de la compra y del XML no coinciden."));

        if (document.PurchaseId.HasValue && document.PurchaseId != purchaseId)
            warnings.Add(new("DocumentAlreadyLinked", "El XML está vinculado a otra compra."));

        if (await dbContext.ElectronicDocuments.AsNoTracking()
                .AnyAsync(x => x.PurchaseId == purchaseId && x.Id != electronicDocumentId, cancellationToken))
            warnings.Add(new("PurchaseAlreadyLinked", "La compra está vinculada a otro XML."));

        var issueDate = DateOnly.FromDateTime(document.FechaEmision.Date);
        if (issueDate != purchase.PurchaseDate)
            warnings.Add(new("DateDifference", "La fecha de emisión del XML difiere de la fecha de compra."));

        if (sameCurrency && document.CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase) &&
            purchase.ExchangeRate.HasValue && document.ExchangeRate > 0 &&
            Math.Abs(document.ExchangeRate - purchase.ExchangeRate.Value) > 0.00001m)
            warnings.Add(new("ExchangeRateDifference", "Los tipos de cambio registrados difieren."));

        decimal? lineCost = purchase.Lines.Count > 0
            ? purchase.Lines.Sum(x => x.Quantity * x.UnitCost)
            : null;
        if (!lineCost.HasValue)
            warnings.Add(new("NoPurchaseLines", "La compra no tiene líneas que permitan una comparación de costos."));
        else if (sameCurrency && Math.Abs(document.TotalNetSale - lineCost.Value) > 1m)
            warnings.Add(new("IndicativeAmountDifference", "El total neto fiscal difiere del costo de las líneas de compra; revise descuentos, impuestos y gastos adicionales."));

        return new PurchaseReconciliationResult(
            electronicDocumentId, purchaseId, supported && sameSupplier && sameCurrency &&
            !warnings.Any(x => x.Code is "DocumentAlreadyLinked" or "PurchaseAlreadyLinked"),
            document.TotalNetSale, document.TotalDocument, lineCost, warnings);
    }
}

public sealed record PurchaseReconciliationWarning(string Code, string Message);
public sealed record PurchaseReconciliationResult(
    int ElectronicDocumentId,
    int PurchaseId,
    bool CanLinkUnderCurrentRules,
    decimal FiscalNetSale,
    decimal FiscalTotal,
    decimal? PurchaseLineCost,
    IReadOnlyList<PurchaseReconciliationWarning> Warnings);
