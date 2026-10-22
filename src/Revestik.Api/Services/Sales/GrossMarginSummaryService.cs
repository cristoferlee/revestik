using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Inventory;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales;

/// <summary>
/// Conservative product-only margin. No stock, sale or cost record is changed.
/// Unknown FIFO costs are excluded from both margin numerator and denominator.
/// </summary>
public sealed class GrossMarginSummaryService(RevestikDbContext db)
{
    public async Task<GrossMarginSummaryResponse> GetAsync(int year, int month, CancellationToken ct)
    {
        if (year is < 2 or > 9998 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
        var start = TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, 1), tz);
        var end = TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, 1).AddMonths(1), tz);

        var sales = await db.Sales.AsNoTracking()
            .Where(x => x.Status == SaleStatus.Issued && x.IssuedAtUtc >= start && x.IssuedAtUtc < end)
            .Include(x => x.Lines).Include(x => x.Charges)
            .ToListAsync(ct);
        var ids = sales.Select(x => x.Id).ToArray();
        var movements = await db.InventoryMovements.AsNoTracking()
            .Where(x => x.SaleId.HasValue && ids.Contains(x.SaleId.Value) &&
                (x.Type == InventoryMovementType.Sale || x.Type == InventoryMovementType.SaleReversal))
            .ToListAsync(ct);
        var movementIds = movements.Select(x => x.Id).ToArray();
        var consumptions = await db.InventoryCostConsumptions.AsNoTracking()
            .Where(x => movementIds.Contains(x.InventoryMovementId))
            .ToListAsync(ct);
        var costsByMovement = consumptions.GroupBy(x => x.InventoryMovementId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var movementGroups = movements.Where(x => x.Type == InventoryMovementType.Sale)
            .GroupBy(x => (x.SaleId!.Value, x.ProductId))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var reversed = movements.Where(x => x.Type == InventoryMovementType.SaleReversal &&
            x.ReversesInventoryMovementId.HasValue)
            .Select(x => x.ReversesInventoryMovementId!.Value).ToHashSet();
        // Match the XML's own date, as done by the existing document-period summary.
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var earliest = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(14));
        var latest = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-14));
        var documents = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.FechaEmision >= earliest && x.FechaEmision < latest)
            .Where(x => x.Direction == ElectronicDocumentDirection.Received &&
                (x.DocumentType == ElectronicDocumentType.Invoice ||
                 x.DocumentType == ElectronicDocumentType.ElectronicTicket ||
                 x.DocumentType == ElectronicDocumentType.PurchaseInvoice))
            .Include(x => x.Lines)
            .ToListAsync(ct);
        var periodDocuments = documents.Where(x =>
            DateOnly.FromDateTime(x.FechaEmision.Date) >= from &&
            DateOnly.FromDateTime(x.FechaEmision.Date) < to).ToList();
        // A note may be issued in another month. Preserve a visible review warning
        // instead of subtracting its tax-inclusive document total from a net cost.
        var periodIds = periodDocuments.Select(x => x.Id).ToArray();
        var notes = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.References.Any(z => z.RelatedElectronicDocumentId.HasValue &&
                periodIds.Contains(z.RelatedElectronicDocumentId.Value)))
            .Where(x => x.Direction == ElectronicDocumentDirection.Received &&
                (x.DocumentType == ElectronicDocumentType.CreditNote ||
                 x.DocumentType == ElectronicDocumentType.DebitNote))
            .Select(x => new { x.Id, x.CurrencyCode, x.AdjustmentStatus,
                RelatedIds = x.References.Select(z => z.RelatedElectronicDocumentId).ToList() })
            .ToListAsync(ct);
        var output = new List<GrossMarginCurrencyResponse>();

        foreach (var currency in new[] { Currency.CRC, Currency.USD })
        {
            decimal verifiedRevenue = 0, verifiedCost = 0, unverifiedRevenue = 0;
            decimal manualLineRevenue = 0, additionalCharges = 0, allLineRevenue = 0;
            int verifiedGroups = 0, unverifiedGroups = 0;
            foreach (var sale in sales.Where(x => x.Currency == currency))
            {
                var lineInputs = sale.Lines.Select(x => new SaleLineRequest
                {
                    ProductId = x.ProductId, CabysCode = x.CabysCode ?? string.Empty,
                    Description = x.Description, Unit = x.Unit, Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice, DiscountType = x.DiscountType,
                    DiscountValue = x.DiscountValue, TaxRate = x.TaxRate
                }).ToList();
                var chargeInputs = sale.Charges.Select(x => new SaleChargeRequest
                {
                    Type = x.Type, Description = x.Description, Amount = x.Amount
                }).ToList();
                var calc = SaleCalculator.Calculate(lineInputs, chargeInputs,
                    sale.GeneralDiscountType, sale.GeneralDiscountValue);
                // Sales are pure line revenue excluding IVA. Charges have no tax breakdown.
                additionalCharges += calc.ChargeTotal;
                allLineRevenue += calc.Subtotal;
                for (var i = 0; i < lineInputs.Count; i++)
                    if (!lineInputs[i].ProductId.HasValue)
                        manualLineRevenue += calc.Lines[i].BaseAmount;

                foreach (var group in lineInputs.Select((line, index) => new { line, index })
                    .Where(x => x.line.ProductId.HasValue)
                    .GroupBy(x => x.line.ProductId!.Value))
                {
                    var revenue = group.Sum(x => calc.Lines[x.index].BaseAmount);
                    var key = (sale.Id, group.Key);
                    if (!movementGroups.TryGetValue(key, out var matched) || matched.Length == 0 ||
                        matched.Any(x => reversed.Contains(x.Id)) ||
                        matched.Any(x => !costsByMovement.TryGetValue(x.Id, out var costs) ||
                            costs.Length == 0 || costs.Any(c => !c.UnitCostSnapshot.HasValue) ||
                            costs.Sum(c => c.Quantity) != Math.Abs(x.QuantityChange)))
                    {
                        unverifiedRevenue += revenue;
                        unverifiedGroups++;
                        continue;
                    }
                    verifiedRevenue += revenue;
                    verifiedCost += matched.Sum(x => costsByMovement[x.Id].Sum(c => c.Quantity * c.UnitCostSnapshot!.Value));
                    verifiedGroups++;
                }
            }
            var margin = Round(verifiedRevenue - verifiedCost);
            var currencyCode = currency.ToString();
            decimal directPurchaseBase = 0;
            var reviewDocuments = 0;
            foreach (var document in periodDocuments.Where(x =>
                string.Equals(x.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)))
            {
                if (document.ProcessingStatus != ElectronicDocumentProcessingStatus.Processed ||
                    document.CategoryId is null)
                {
                    reviewDocuments++;
                    continue;
                }
                // Line overrides determine effective destination, including mixed XMLs.
                // Net amounts exclude tax. Do not substitute total document (tax included).
                var hasOverrides = document.Lines.Any(x =>
                    x.ClassificationCategoryId.HasValue || x.OperationalDestination.HasValue);
                var hasDirect = hasOverrides
                    ? document.Lines.Any(x => (x.OperationalDestination ??
                        document.OperationalDestination) == OperationalDestination.DirectCustomer)
                    : document.OperationalDestination == OperationalDestination.DirectCustomer;
                if (!hasDirect) continue;
                var hasNote = notes.Any(x =>
                    string.Equals(x.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase) &&
                    x.RelatedIds.Contains(document.Id));
                if (hasNote) reviewDocuments++;
                if (!hasOverrides)
                {
                    directPurchaseBase += document.TotalNetSale;
                }
                else
                {
                    directPurchaseBase += document.Lines
                        .Where(x => (x.OperationalDestination ?? document.OperationalDestination) ==
                            OperationalDestination.DirectCustomer)
                        .Sum(x => x.Subtotal);
                    // Unallocated differences/rounding or other charges aren't silently
                    // assigned to direct-purchase cost.
                    if (document.TotalNetSale != document.Lines.Sum(x => x.Subtotal))
                        reviewDocuments++;
                }
            }
            // An aggregate by XML issue month is a cashless purchasing approximation;
            // direct-purchase expenses may precede/follow the corresponding sale.
            var result = Round(allLineRevenue - verifiedCost - directPurchaseBase);
            var requiresReview = reviewDocuments > 0 || unverifiedGroups > 0 ||
                manualLineRevenue != 0m || additionalCharges != 0m;
            output.Add(new GrossMarginCurrencyResponse(currency, Round(verifiedRevenue),
                Round(verifiedCost), margin,
                verifiedRevenue == 0m ? null : Round(100m * margin / verifiedRevenue),
                Round(unverifiedRevenue), unverifiedGroups,
                Round(manualLineRevenue), Round(additionalCharges), verifiedGroups,
                Round(directPurchaseBase), Round(manualLineRevenue), Round(allLineRevenue),
                result, requiresReview, reviewDocuments));
        }
        return new GrossMarginSummaryResponse(year, month, output);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
