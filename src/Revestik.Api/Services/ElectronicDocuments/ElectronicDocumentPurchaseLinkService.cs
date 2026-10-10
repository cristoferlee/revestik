using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Establishes a fiscal-evidence link to an existing operational purchase.
/// Does not create purchases, payments, expenses, stock movements, or cost layers.
/// </summary>
public sealed class ElectronicDocumentPurchaseLinkService(RevestikDbContext dbContext)
{
    public async Task LinkAsync(int electronicDocumentId, int purchaseId,
        CancellationToken cancellationToken = default)
    {
        if (electronicDocumentId <= 0 || purchaseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(electronicDocumentId), "Los identificadores deben ser positivos.");

        var document = await dbContext.ElectronicDocuments
            .SingleOrDefaultAsync(x => x.Id == electronicDocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("El documento fiscal no existe.");

        if (document.Direction != ElectronicDocumentDirection.Received ||
            document.DocumentType is not (ElectronicDocumentType.Invoice
                or ElectronicDocumentType.ElectronicTicket
                or ElectronicDocumentType.PurchaseInvoice
                or ElectronicDocumentType.ExportInvoice))
            throw new InvalidOperationException("Este tipo de documento no puede vincularse directamente a una compra recibida.");

        if (document.PurchaseId == purchaseId)
            return;

        if (document.PurchaseId.HasValue)
            throw new InvalidOperationException("El documento ya tiene una compra vinculada.");

        var purchase = await dbContext.Purchases
            .Include(x => x.Supplier)
            .SingleOrDefaultAsync(x => x.Id == purchaseId, cancellationToken)
            ?? throw new KeyNotFoundException("La compra no existe.");

        var alreadyLinked = await dbContext.ElectronicDocuments.AsNoTracking()
            .AnyAsync(x => x.PurchaseId == purchaseId && x.Id != electronicDocumentId, cancellationToken);
        if (alreadyLinked)
            throw new InvalidOperationException("La compra ya está vinculada a otro documento fiscal.");

        if (string.IsNullOrWhiteSpace(document.IssuerIdentification) ||
            !string.Equals(document.IssuerIdentificationType, purchase.Supplier.IdentificationType, StringComparison.Ordinal) ||
            !string.Equals(document.IssuerIdentification, purchase.Supplier.IdentificationNumber, StringComparison.Ordinal))
            throw new InvalidOperationException("La identificación fiscal del emisor no coincide con la del proveedor de la compra.");

        var expectedCurrency = purchase.Currency == PurchaseCurrency.CRC ? "CRC" : "USD";
        if (!string.Equals(document.CurrencyCode, expectedCurrency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La moneda del documento fiscal no coincide con la de la compra.");

        // Comparison of fiscal totals against purchase costs requires tax/discount
        // reconciliation (Block 5.3). No monetary equality is presumed here.
        document.PurchaseId = purchaseId;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique filtered PurchaseId index is the final concurrency guard.
            // Reset the tracked link so an unsuccessful attempt cannot be retried implicitly.
            document.PurchaseId = null;
            dbContext.Entry(document).Property(x => x.PurchaseId).IsModified = false;
            throw new InvalidOperationException(
                "La compra fue vinculada por otra operación concurrente.", exception);
        }
    }
}
