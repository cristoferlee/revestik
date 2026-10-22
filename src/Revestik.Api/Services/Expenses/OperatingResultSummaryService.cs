using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.Sales;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Expenses;

/// <summary>
/// Read-only, conservative operational projection. Does not alter the existing
/// consolidated expense report or attempt to deduplicate unlinked payments.
/// </summary>
public sealed class OperatingResultSummaryService(
    RevestikDbContext db, GrossMarginSummaryService commercialService)
{
    public async Task<OperatingResultSummaryResponse> GetAsync(int year, int month, CancellationToken ct)
    {
        var commercial = await commercialService.GetAsync(year, month, ct);
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var earliest = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(14));
        var latest = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-14));
        var documents = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.FechaEmision >= earliest && x.FechaEmision < latest &&
                x.Direction == ElectronicDocumentDirection.Received &&
                (x.DocumentType == ElectronicDocumentType.Invoice ||
                 x.DocumentType == ElectronicDocumentType.ElectronicTicket ||
                 x.DocumentType == ElectronicDocumentType.PurchaseInvoice))
            .Select(x => new
            {
                x.Id, x.FechaEmision, x.CurrencyCode, x.ProcessingStatus,
                x.CategoryId, x.OperationalDestination, x.TotalNetSale,
                AccountingNature = x.Category == null ? (AccountingNature?)null : x.Category.AccountingNature,
                Lines = x.Lines.Select(line => new
                {
                    line.Subtotal, line.ClassificationCategoryId, line.OperationalDestination,
                    LineNature = line.ClassificationCategory == null ? (AccountingNature?)null : line.ClassificationCategory.AccountingNature
                }).ToList()
            }).ToListAsync(ct);
        var currentDocuments = documents.Where(x =>
            DateOnly.FromDateTime(x.FechaEmision.Date) >= from &&
            DateOnly.FromDateTime(x.FechaEmision.Date) < to).ToList();
        var ids = currentDocuments.Select(x => x.Id).ToArray();
        var adjustedOriginalIds = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.Direction == ElectronicDocumentDirection.Received &&
                (x.DocumentType == ElectronicDocumentType.CreditNote ||
                 x.DocumentType == ElectronicDocumentType.DebitNote) &&
                x.References.Any(r => r.RelatedElectronicDocumentId.HasValue &&
                    ids.Contains(r.RelatedElectronicDocumentId.Value)))
            .SelectMany(x => x.References
                .Where(r => r.RelatedElectronicDocumentId.HasValue && ids.Contains(r.RelatedElectronicDocumentId.Value))
                .Select(r => r.RelatedElectronicDocumentId!.Value))
            .Distinct().ToListAsync(ct);
        var adjustedIdSet = adjustedOriginalIds.ToHashSet();
        var manualTotal = await db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= from && x.ExpenseDate < to)
            .SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), tz);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.ToDateTime(TimeOnly.MinValue), tz);
        var vouchers = await db.BankVouchers.AsNoTracking()
            .Where(x => x.TransactionDate >= new DateTimeOffset(start) &&
                x.TransactionDate < new DateTimeOffset(end) &&
                x.Status == BankVoucherStatus.Accepted)
            .GroupBy(x => x.Currency)
            .Select(x => new { Currency = x.Key, Amount = x.Sum(z => z.Amount) })
            .ToListAsync(ct);
        var output = new List<OperatingResultCurrencyResponse>();
        foreach (var item in commercial.Currencies)
        {
            var currency = item.Currency.ToString();
            decimal eligible = 0m;
            int requiringReview = 0;
            foreach (var doc in currentDocuments.Where(x =>
                string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)))
            {
                if (doc.ProcessingStatus != ElectronicDocumentProcessingStatus.Processed ||
                    doc.CategoryId == null || doc.AccountingNature == null)
                {
                    requiringReview++;
                    continue;
                }
                var overrides = doc.Lines.Any(x => x.ClassificationCategoryId != null || x.OperationalDestination != null);
                var isExpense = overrides
                    ? doc.Lines.Any(x => OperatingExpenseEligibility.IsEligible(
                        x.LineNature ?? doc.AccountingNature.Value,
                        x.OperationalDestination ?? doc.OperationalDestination))
                    : OperatingExpenseEligibility.IsEligible(doc.AccountingNature.Value, doc.OperationalDestination);
                if (!isExpense) continue;
                // A linked note may affect only part of a mixed document.
                // Exclude that expense until it can be allocated without guessing.
                if (adjustedIdSet.Contains(doc.Id)) { requiringReview++; continue; }
                if (!overrides)
                {
                    eligible += doc.TotalNetSale;
                }
                else
                {
                    eligible += doc.Lines.Where(x => OperatingExpenseEligibility.IsEligible(
                        x.LineNature ?? doc.AccountingNature.Value,
                        x.OperationalDestination ?? doc.OperationalDestination))
                        .Sum(x => x.Subtotal);
                    if (doc.Lines.Sum(x => x.Subtotal) != doc.TotalNetSale)
                        requiringReview++;
                }
            }
            eligible = Round(eligible);
            var accepted = vouchers.Where(x => string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);
            // Manual expenses have no XML linkage in the current model. Keep them
            // visible but do not imply verified nonduplication.
            var manual = currency == "CRC" ? manualTotal : 0m;
            output.Add(new OperatingResultCurrencyResponse(
                currency, item.EstimatedCommercialResult, eligible,
                Round(item.EstimatedCommercialResult - eligible), Round(manual), Round(accepted),
                requiringReview, item.RequiresReview || requiringReview > 0 || manual != 0m || accepted != 0m));
        }
        return new OperatingResultSummaryResponse(year, month, output);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
