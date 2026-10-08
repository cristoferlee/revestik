using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.Expenses;

public sealed class ExpenseReportingService(RevestikDbContext dbContext)
    : IExpenseReportingService
{
    public async Task<ExpenseConsolidatedSummaryResponse> GetConsolidatedSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totals = new Dictionary<string, CurrencyAccumulator>(
            StringComparer.OrdinalIgnoreCase);

        var manualQuery = dbContext.Expenses.AsNoTracking();

        if (request.DateFrom.HasValue)
            manualQuery = manualQuery.Where(x => x.ExpenseDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            manualQuery = manualQuery.Where(x => x.ExpenseDate <= request.DateTo.Value);

        var manualTotal = await manualQuery
            .Select(x => (decimal?)x.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;

        GetAccumulator(totals, "CRC").ManualExpenses += manualTotal;

        var documentQuery = dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x =>
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null);

        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            documentQuery = documentQuery.Where(x => x.FechaEmision >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            documentQuery = documentQuery.Where(x => x.FechaEmision < until);
        }

        var documents = await documentQuery
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                DocumentNature = x.Category!.AccountingNature,
                Lines = x.Lines
                    .Select(line => new
                    {
                        line.TotalLine,
                        HasOverride =
                            line.ClassificationCategoryId != null ||
                            line.OperationalDestination != null,
                        EffectiveNature =
                            line.ClassificationCategory != null
                                ? line.ClassificationCategory.AccountingNature
                                : x.Category!.AccountingNature
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var document in documents)
        {
            var currency = string.IsNullOrWhiteSpace(document.CurrencyCode)
                ? "N/D"
                : document.CurrencyCode.Trim().ToUpperInvariant();

            var accumulator = GetAccumulator(totals, currency);

            if (!document.Lines.Any(line => line.HasOverride))
            {
                if (document.DocumentNature == AccountingNature.OperatingExpense)
                    accumulator.ElectronicDocumentExpenses += document.TotalDocument;

                continue;
            }

            foreach (var line in document.Lines)
            {
                if (line.EffectiveNature == AccountingNature.OperatingExpense)
                    accumulator.ElectronicDocumentExpenses += line.TotalLine;
            }
        }

        var voucherQuery = dbContext.BankVouchers.AsNoTracking();

        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            voucherQuery = voucherQuery.Where(x => x.TransactionDate >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            voucherQuery = voucherQuery.Where(x => x.TransactionDate < until);
        }

        var voucherSummary = await voucherQuery
            .GroupBy(x => new { x.Currency, x.Status })
            .Select(group => new
            {
                group.Key.Currency,
                group.Key.Status,
                Count = group.Count(),
                Amount = group.Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var needsReview = 0;
        var accepted = 0;
        var matched = 0;
        var ignored = 0;

        foreach (var group in voucherSummary)
        {
            switch (group.Status)
            {
                case BankVoucherStatus.Accepted:
                    accepted += group.Count;
                    GetAccumulator(
                        totals,
                        NormalizeCurrency(group.Currency))
                        .AcceptedBankVouchers += group.Amount;
                    break;

                case BankVoucherStatus.NeedsReview:
                    needsReview += group.Count;
                    break;

                case BankVoucherStatus.Matched:
                    matched += group.Count;
                    break;

                case BankVoucherStatus.Ignored:
                    ignored += group.Count;
                    break;
            }
        }

        var result = totals
            .Where(x =>
                x.Value.ManualExpenses != 0m ||
                x.Value.ElectronicDocumentExpenses != 0m ||
                x.Value.AcceptedBankVouchers != 0m)
            .OrderBy(x => x.Key)
            .Select(x => new ExpenseConsolidatedCurrencyTotalResponse(
                x.Key,
                x.Value.ManualExpenses,
                x.Value.ElectronicDocumentExpenses,
                x.Value.AcceptedBankVouchers,
                x.Value.ManualExpenses +
                x.Value.ElectronicDocumentExpenses +
                x.Value.AcceptedBankVouchers))
            .ToList();

        return new ExpenseConsolidatedSummaryResponse(
            result,
            needsReview,
            accepted,
            matched,
            ignored);
    }

    private static string NormalizeCurrency(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "N/D"
            : value.Trim().ToUpperInvariant();

    private static CurrencyAccumulator GetAccumulator(
        IDictionary<string, CurrencyAccumulator> totals,
        string currency)
    {
        if (!totals.TryGetValue(currency, out var accumulator))
        {
            accumulator = new CurrencyAccumulator();
            totals[currency] = accumulator;
        }

        return accumulator;
    }

    private sealed class CurrencyAccumulator
    {
        public decimal ManualExpenses { get; set; }
        public decimal ElectronicDocumentExpenses { get; set; }
        public decimal AcceptedBankVouchers { get; set; }
    }
}
