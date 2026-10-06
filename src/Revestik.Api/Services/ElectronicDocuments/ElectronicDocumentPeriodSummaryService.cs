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

        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(x => x.FechaEmision >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(x => x.FechaEmision < until);
        }

        var pending = await query.CountAsync(
            x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Pending,
            cancellationToken);
        var processed = await query.CountAsync(
            x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed,
            cancellationToken);
        var noAction = await query.CountAsync(
            x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.NoActionRequired,
            cancellationToken);
        var withHacienda = await query.CountAsync(
            x => x.HaciendaResponse != null,
            cancellationToken);

        var classified = await query
            .Where(x =>
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null)
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                DocumentNature = x.Category!.AccountingNature,
                Lines = x.Lines
                    .Select(line => new
                    {
                        line.TotalLine,
                        HasOverride = line.ClassificationCategoryId != null || line.OperationalDestination != null,
                        EffectiveNature = line.ClassificationCategory != null
                            ? line.ClassificationCategory.AccountingNature
                            : x.Category!.AccountingNature
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<string, AccountingAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in classified)
        {
            var currency = string.IsNullOrWhiteSpace(document.CurrencyCode)
                ? "N/D"
                : document.CurrencyCode;

            if (!totals.TryGetValue(currency, out var accumulator))
            {
                accumulator = new AccountingAccumulator();
                totals[currency] = accumulator;
            }

            if (!document.Lines.Any(x => x.HasOverride))
            {
                accumulator.Add(document.DocumentNature, document.TotalDocument);
                continue;
            }

            var allocated = 0m;
            foreach (var line in document.Lines)
            {
                accumulator.Add(line.EffectiveNature, line.TotalLine);
                allocated += line.TotalLine;
            }

            accumulator.Unallocated += document.TotalDocument - allocated;
        }

        var accountingTotals = totals
            .OrderBy(x => x.Key)
            .Select(x => new ElectronicDocumentAccountingTotalResponse(
                x.Key,
                x.Value.Purchases,
                x.Value.Expenses,
                x.Value.DirectCosts,
                x.Value.Assets,
                x.Value.Unallocated))
            .ToList();

        var financialSource = await query
            .Where(x =>
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null)
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                x.SaleConditionCode
            })
            .ToListAsync(cancellationToken);

        var financialTotals = financialSource
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CurrencyCode) ? "N/D" : x.CurrencyCode)
            .OrderBy(group => group.Key)
            .Select(group => new ElectronicDocumentFinancialTotalResponse(
                group.Key,
                group.Where(x => x.SaleConditionCode == "01").Sum(x => x.TotalDocument),
                group.Where(x => x.SaleConditionCode == "02").Sum(x => x.TotalDocument),
                group.Where(x => x.SaleConditionCode != "01" && x.SaleConditionCode != "02").Sum(x => x.TotalDocument)))
            .ToList();

        return new ElectronicDocumentSummaryResponse(
            pending,
            processed,
            noAction,
            withHacienda,
            accountingTotals,
            financialTotals);
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
