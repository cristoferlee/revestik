using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ElectronicDocumentPeriodSummaryService(RevestikDbContext dbContext)
{
    public async Task<ElectronicDocumentSummaryResponse> GetSummaryAsync(
        ElectronicDocumentSummaryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.ElectronicDocuments.AsNoTracking();

        if (request.DateFrom.HasValue || request.DateTo.HasValue)
        {
            // Preserve the XML's calendar date, including its original UTC offset.
            // SQL Server compares datetimeoffset instants: first fetch a conservative
            // candidate window and then validate each XML-local date in memory.
            var candidates = query;
            if (request.DateFrom.HasValue)
            {
                var earliestInstant = new DateTimeOffset(
                    request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                    TimeSpan.FromHours(14));
                candidates = candidates.Where(x => x.FechaEmision >= earliestInstant);
            }

            if (request.DateTo.HasValue)
            {
                var latestExclusiveInstant = new DateTimeOffset(
                    request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                    TimeSpan.FromHours(-14));
                candidates = candidates.Where(x => x.FechaEmision < latestExclusiveInstant);
            }

            var eligibleIds = (await candidates
                .Select(x => new { x.Id, x.FechaEmision })
                .ToListAsync(cancellationToken))
                .Where(x =>
                {
                    var date = DateOnly.FromDateTime(x.FechaEmision.Date);
                    return (!request.DateFrom.HasValue || date >= request.DateFrom.Value) &&
                           (!request.DateTo.HasValue || date <= request.DateTo.Value);
                })
                .Select(x => x.Id)
                .ToArray();

            query = query.Where(x => eligibleIds.Contains(x.Id));
        }

        // REFACTOR-04D.2: keep the original fiscal-period filter, but replace
        // four independent COUNT commands with one conditional aggregation.
        // The grouped query returns null when the selected period is empty.
        var counts = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Pending = group.Count(x =>
                    x.ProcessingStatus == ElectronicDocumentProcessingStatus.Pending),
                Processed = group.Count(x =>
                    x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed),
                NoAction = group.Count(x =>
                    x.ProcessingStatus == ElectronicDocumentProcessingStatus.NoActionRequired),
                WithHacienda = group.Count(x => x.HaciendaResponse != null)
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Counts include all fiscal evidence. Accounting and financial totals
        // exclude issued documents, notes, receipts, and unclassified documents.
        var classified = await query
            .Where(x =>
                x.Direction == ElectronicDocumentDirection.Received &&
                (x.DocumentType == ElectronicDocumentType.Invoice ||
                 x.DocumentType == ElectronicDocumentType.ElectronicTicket ||
                 x.DocumentType == ElectronicDocumentType.PurchaseInvoice) &&
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null)
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                x.SaleConditionCode,
                DocumentNature = x.Category!.AccountingNature,
                Lines = x.Lines
                    .Select(line => new
                    {
                        line.TotalLine,
                        HasOverride = line.ClassificationCategoryId != null ||
                                      line.OperationalDestination != null,
                        EffectiveNature = line.ClassificationCategory != null
                            ? line.ClassificationCategory.AccountingNature
                            : x.Category!.AccountingNature
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Match the original comparer semantics: accounting currencies are grouped
        // ignoring case; financial currencies are grouped with ordinal case.
        var accountingByCurrency = new Dictionary<string, AccountingAccumulator>(
            StringComparer.OrdinalIgnoreCase);
        var financialByCurrency = new Dictionary<string,
            (decimal Cash, decimal Credit, decimal Other)>(StringComparer.Ordinal);

        foreach (var document in classified)
        {
            var currency = string.IsNullOrWhiteSpace(document.CurrencyCode)
                ? "N/D"
                : document.CurrencyCode;

            if (!accountingByCurrency.TryGetValue(currency, out var accounting))
            {
                accounting = new AccountingAccumulator();
                accountingByCurrency[currency] = accounting;
            }

            financialByCurrency.TryGetValue(currency, out var financial);
            financialByCurrency[currency] = document.SaleConditionCode switch
            {
                "01" => (financial.Cash + document.TotalDocument,
                         financial.Credit, financial.Other),
                "02" => (financial.Cash,
                         financial.Credit + document.TotalDocument, financial.Other),
                _ => (financial.Cash, financial.Credit,
                      financial.Other + document.TotalDocument)
            };

            // A document without line overrides keeps its document-level nature
            // and full total; otherwise distribute line totals and the remainder.
            if (!document.Lines.Any(line => line.HasOverride))
            {
                accounting.Add(document.DocumentNature, document.TotalDocument);
                continue;
            }

            var allocated = 0m;
            foreach (var line in document.Lines)
            {
                accounting.Add(line.EffectiveNature, line.TotalLine);
                allocated += line.TotalLine;
            }

            accounting.Unallocated += document.TotalDocument - allocated;
        }

        var accountingTotals = accountingByCurrency
            .OrderBy(x => x.Key)
            .Select(x => new ElectronicDocumentAccountingTotalResponse(
                x.Key,
                x.Value.Purchases,
                x.Value.Expenses,
                x.Value.DirectCosts,
                x.Value.Assets,
                x.Value.Unallocated))
            .ToList();

        var financialTotals = financialByCurrency
            .OrderBy(x => x.Key)
            .Select(x => new ElectronicDocumentFinancialTotalResponse(
                x.Key,
                x.Value.Cash,
                x.Value.Credit,
                x.Value.Other))
            .ToList();

        return new ElectronicDocumentSummaryResponse(
            counts?.Pending ?? 0,
            counts?.Processed ?? 0,
            counts?.NoAction ?? 0,
            counts?.WithHacienda ?? 0,
            accountingTotals,
            financialTotals);
    }

    // Independent documentary counter. Existing accounting/financial summaries retain
    // their original meaning and are not added to these totals.
    public async Task<FiscalDocumentCounterResponse> GetDocumentCounterAsync(
        ElectronicDocumentSummaryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DateFrom.HasValue && request.DateTo.HasValue &&
            request.DateFrom.Value > request.DateTo.Value)
            throw new ArgumentException("El rango de fechas es inválido.", nameof(request));

        // SQL Server compares datetimeoffset values by instant, while the fiscal
        // period is based on the calendar date recorded in the XML's own offset.
        // An offset can range from -14:00 to +14:00. Use a *conservative* SQL
        // instant window (a superset), then retain the exact calendar-date check
        // below. This avoids loading all historical documents for a short period
        // without silently moving an XML into a different fiscal day.
        var counterQuery = dbContext.ElectronicDocuments.AsNoTracking();
        if (request.DateFrom.HasValue)
        {
            var earliestInstant = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(14));
            counterQuery = counterQuery.Where(x => x.FechaEmision >= earliestInstant);
        }
        if (request.DateTo.HasValue)
        {
            var latestExclusiveInstant = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-14));
            counterQuery = counterQuery.Where(x => x.FechaEmision < latestExclusiveInstant);
        }

        var documents = await counterQuery
            .Select(x => new
            {
                x.Id, x.Clave, x.FechaEmision, x.CurrencyCode,
                x.DocumentType, x.Direction, x.AdjustmentStatus,
                x.TotalDocument
            })
            .ToListAsync(cancellationToken);

        var rows = new List<FiscalDocumentCounterItem>();
        foreach (var document in documents)
        {
            var date = DateOnly.FromDateTime(document.FechaEmision.Date);
            if ((request.DateFrom.HasValue && date < request.DateFrom.Value) ||
                (request.DateTo.HasValue && date > request.DateTo.Value))
                continue;

            var currency = string.IsNullOrWhiteSpace(document.CurrencyCode)
                ? "N/D" : document.CurrencyCode.Trim().ToUpperInvariant();
            var eligible = document.Direction == ElectronicDocumentDirection.Received &&
                document.TotalDocument >= 0m;
            var signed = 0m;
            string? reason = null;

            if (!eligible)
                reason = "Documento emitido o importe inválido";
            else if (currency != "CRC" && currency != "USD")
                reason = "Moneda no admitida para el contador";
            else if (document.DocumentType is ElectronicDocumentType.Invoice or
                     ElectronicDocumentType.ElectronicTicket or
                     ElectronicDocumentType.PurchaseInvoice)
                signed = document.TotalDocument;
            else if (document.DocumentType is ElectronicDocumentType.CreditNote or
                     ElectronicDocumentType.DebitNote)
            {
                if (document.AdjustmentStatus is ElectronicDocumentAdjustmentStatus.Approved or
                    ElectronicDocumentAdjustmentStatus.Applied)
                    signed = document.DocumentType == ElectronicDocumentType.CreditNote
                        ? -document.TotalDocument : document.TotalDocument;
                else
                    reason = "Nota pendiente de aprobación fiscal";
            }
            else
                reason = "Tipo documental informativo o no elegible";

            rows.Add(new FiscalDocumentCounterItem(
                document.Id, document.Clave, date, currency, document.DocumentType,
                document.TotalDocument, reason is null ? signed : 0m,
                reason is null, reason));
        }

        var totals = rows.Where(x => x.IsIncluded)
            .GroupBy(x => x.CurrencyCode)
            .OrderBy(x => x.Key)
            .Select(group => new FiscalDocumentCounterCurrencyTotal(
                group.Key,
                group.Where(x => x.DocumentType is ElectronicDocumentType.Invoice or
                    ElectronicDocumentType.ElectronicTicket or
                    ElectronicDocumentType.PurchaseInvoice).Sum(x => x.SignedTotal),
                -group.Where(x => x.DocumentType == ElectronicDocumentType.CreditNote).Sum(x => x.SignedTotal),
                group.Where(x => x.DocumentType == ElectronicDocumentType.DebitNote).Sum(x => x.SignedTotal),
                group.Sum(x => x.SignedTotal)))
            .ToList();

        return new FiscalDocumentCounterResponse(totals,
            rows.OrderBy(x => x.CurrencyCode).ThenBy(x => x.IssueDate).ThenBy(x => x.DocumentId).ToList());
    }

    private sealed class AccountingAccumulator
    {
        public decimal Purchases { get; private set; }
        public decimal Expenses { get; private set; }
        public decimal DirectCosts { get; private set; }
        public decimal Assets { get; private set; }
        public decimal Unallocated { get; set; }

        public void Add(AccountingNature nature, decimal amount)
        {
            switch (nature)
            {
                case AccountingNature.Inventory:
                    Purchases += amount;
                    break;
                case AccountingNature.OperatingExpense:
                    Expenses += amount;
                    break;
                case AccountingNature.DirectCost:
                    DirectCosts += amount;
                    break;
                case AccountingNature.FixedAsset:
                    Assets += amount;
                    break;
                default:
                    Unallocated += amount;
                    break;
            }
        }
    }
}
