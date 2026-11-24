using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.BankVouchers;

public sealed class BankVoucherReviewService(RevestikDbContext dbContext)
    : IBankVoucherReviewService
{
    private static readonly TimeSpan MatchWindow = TimeSpan.FromDays(3);

    public async Task<BankVoucherPageResponse> GetAsync(
        BankVoucherListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Cache is scoped to this list request, never shared across users or requests.
        var lookups = new BatchLookupContext(dbContext);
        await RequeueAcceptedWithLateCandidatesAsync(
            request, lookups, cancellationToken);

        var vouchersQuery = ApplyDateRange(
            dbContext.BankVouchers.AsNoTracking(), request);

        if (request.Status.HasValue)
        {
            vouchersQuery = vouchersQuery.Where(x => x.Status == request.Status.Value);
        }
        else if (request.ExcludeNeedsReview)
        {
            vouchersQuery = vouchersQuery.Where(x => x.Status != BankVoucherStatus.NeedsReview);
        }

        var totalCount = await vouchersQuery.CountAsync(cancellationToken);
        var vouchers = await vouchersQuery
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var results = new List<BankVoucherReviewItemResponse>(vouchers.Count);
        foreach (var batch in vouchers.Chunk(BatchSize))
        {
            var analyses = await AnalyzeBatchAsync(batch, lookups, cancellationToken);
            for (var index = 0; index < batch.Length; index++)
                results.Add(Map(batch[index], analyses[index]));
        }

        return new BankVoucherPageResponse(results, totalCount, request.Page, request.PageSize);
    }

    public async Task<BankVoucherReviewItemResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var voucher = await dbContext.BankVouchers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (voucher is null)
            return null;

        var analysis = await AnalyzeAsync(voucher, cancellationToken);

        if (voucher.Status == BankVoucherStatus.Accepted &&
            analysis.Candidates.Count > 0)
        {
            voucher.Status = BankVoucherStatus.NeedsReview;
            voucher.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(voucher, analysis);
    }

    public async Task<bool> AcceptAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var voucher = await dbContext.BankVouchers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (voucher is null)
            return false;

        voucher.Status = BankVoucherStatus.Accepted;
        voucher.MatchedElectronicDocumentId = null;
        voucher.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IgnoreAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var voucher = await dbContext.BankVouchers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (voucher is null)
            return false;

        voucher.Status = BankVoucherStatus.Ignored;
        voucher.MatchedElectronicDocumentId = null;
        voucher.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MatchAsync(
        int id,
        int electronicDocumentId,
        CancellationToken cancellationToken)
    {
        var voucher = await dbContext.BankVouchers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (voucher is null)
            return false;

        var analysis = await AnalyzeAsync(voucher, cancellationToken);

        if (!analysis.Candidates.Any(x =>
                x.ElectronicDocumentId == electronicDocumentId))
        {
            return false;
        }

        voucher.Status = BankVoucherStatus.Matched;
        voucher.MatchedElectronicDocumentId = electronicDocumentId;
        voucher.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RequeueAcceptedWithLateCandidatesAsync(
        BankVoucherListRequest request,
        BatchLookupContext lookups,
        CancellationToken cancellationToken)
    {
        // Deliberately preserve full date-scoped reconciliation until 02A.4.
        var acceptedQuery = ApplyDateRange(
            dbContext.BankVouchers, request)
            .Where(x => x.Status == BankVoucherStatus.Accepted);

        var accepted = await acceptedQuery
            .OrderByDescending(x => x.TransactionDate)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var batch in accepted.Chunk(BatchSize))
        {
            var analyses = await AnalyzeBatchAsync(batch, lookups, cancellationToken);
            for (var index = 0; index < batch.Length; index++)
            {
                if (analyses[index].Candidates.Count == 0)
                    continue;
                batch[index].Status = BankVoucherStatus.NeedsReview;
                batch[index].UpdatedAtUtc = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<BankVoucher> ApplyDateRange(
        IQueryable<BankVoucher> query,
        BankVoucherListRequest request)
    {
        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            query = query.Where(x =>
                x.TransactionDate >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1)
                    .ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            query = query.Where(x =>
                x.TransactionDate < until);
        }

        return query;
    }

    // The single-item operations retain the same matching rules as list operations.
    private async Task<VoucherAnalysis> AnalyzeAsync(
        BankVoucher voucher, CancellationToken cancellationToken)
    {
        var analyses = await AnalyzeBatchAsync(
            [voucher], new BatchLookupContext(dbContext), cancellationToken);
        return analyses[0];
    }

    private const int BatchSize = 25;

    private async Task<IReadOnlyList<VoucherAnalysis>> AnalyzeBatchAsync(
        IReadOnlyList<BankVoucher> vouchers,
        BatchLookupContext lookups,
        CancellationToken cancellationToken)
    {
        if (vouchers.Count == 0)
            return [];

        var suppliers = await lookups.GetSuppliersAsync(cancellationToken);
        var amounts = vouchers.Select(x => x.Amount).Distinct().ToArray();
        var currencies = vouchers.Select(x => x.Currency).Distinct().ToArray();
        var earliest = vouchers.Min(x => x.TransactionDate) - MatchWindow;
        var latest = vouchers.Max(x => x.TransactionDate) + MatchWindow;

        // Superset query only. Reapply every voucher's exact amount, currency,
        // time window, and normalized merchant compatibility below.
        var documents = await dbContext.ElectronicDocuments.AsNoTracking()
            .Where(x =>
                amounts.Contains(x.TotalDocument) &&
                currencies.Contains(x.CurrencyCode) &&
                x.FechaEmision >= earliest && x.FechaEmision <= latest)
            .Select(x => new
            {
                x.Id,
                x.IssuerName,
                x.IssuerCommercialName,
                x.FechaEmision,
                x.TotalDocument,
                x.CurrencyCode,
                AccountingNature = x.Category != null
                    ? (AccountingNature?)x.Category.AccountingNature
                    : null,
                CategoryName = x.Category != null
                    ? x.Category.Name
                    : null
            })
            .ToListAsync(cancellationToken);

        var candidateKeys = documents.Select(x => new
        {
            Document = x,
            IssuerKey = NormalizeName(x.IssuerName),
            CommercialKey = NormalizeName(x.IssuerCommercialName)
        }).ToList();

        var analyses = new List<VoucherAnalysis>(vouchers.Count);
        foreach (var voucher in vouchers)
        {
            var merchantKey = NormalizeName(voucher.MerchantName);
            var knownSupplier = suppliers.Any(x =>
                NamesCompatible(merchantKey, x.Name) ||
                NamesCompatible(merchantKey, x.CommercialName));

            var from = voucher.TransactionDate - MatchWindow;
            var until = voucher.TransactionDate + MatchWindow;

            var compatible = candidateKeys
                .Where(x =>
                    x.Document.TotalDocument == voucher.Amount &&
                    x.Document.CurrencyCode == voucher.Currency &&
                    x.Document.FechaEmision >= from &&
                    x.Document.FechaEmision <= until &&
                    (NamesCompatible(merchantKey, x.IssuerKey) ||
                     NamesCompatible(merchantKey, x.CommercialKey)))
                .OrderBy(x => Math.Abs(
                    (x.Document.FechaEmision - voucher.TransactionDate).TotalMinutes))
                .Select(x => new BankVoucherMatchCandidateResponse(
                    x.Document.Id,
                    x.Document.IssuerName,
                    string.IsNullOrWhiteSpace(x.Document.IssuerCommercialName)
                        ? null
                        : x.Document.IssuerCommercialName,
                    x.Document.FechaEmision,
                    x.Document.TotalDocument,
                    x.Document.CurrencyCode,
                    x.Document.AccountingNature,
                    x.Document.CategoryName))
                .ToList();

            // Preserve the original historical-issuer fallback. Memoization avoids
            // repeated full-history queries for the same normalized merchant.
            var knownIssuer = compatible.Count > 0 || knownSupplier ||
                await lookups.GetKnownIssuerAsync(merchantKey, cancellationToken);

            analyses.Add(new VoucherAnalysis(knownSupplier || knownIssuer, compatible));
        }
        return analyses;
    }

    private sealed class BatchLookupContext(RevestikDbContext db)
    {
        private IReadOnlyList<(string Name, string CommercialName)>? suppliers;
        private IReadOnlyList<(string IssuerName, string CommercialName)>? issuers;
        private readonly Dictionary<string, bool> issuerMatches = new(StringComparer.Ordinal);

        public async Task<IReadOnlyList<(string Name, string CommercialName)>> GetSuppliersAsync(
            CancellationToken cancellationToken)
        {
            if (suppliers is not null)
                return suppliers;

            var rows = await db.Suppliers.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new { x.Name, x.CommercialName })
                .ToListAsync(cancellationToken);

            suppliers = rows.Select(x =>
                (Name: NormalizeName(x.Name),
                 CommercialName: NormalizeName(x.CommercialName)))
                .ToList();
            return suppliers;
        }

        public async Task<bool> GetKnownIssuerAsync(
            string merchantKey, CancellationToken cancellationToken)
        {
            if (issuerMatches.TryGetValue(merchantKey, out var result))
                return result;

            if (issuers is null)
            {
                var rows = await db.ElectronicDocuments.AsNoTracking()
                    .Select(x => new { x.IssuerName, x.IssuerCommercialName })
                    .ToListAsync(cancellationToken);

                issuers = rows.Select(x =>
                    (IssuerName: NormalizeName(x.IssuerName),
                     CommercialName: NormalizeName(x.IssuerCommercialName)))
                    .ToList();
            }

            result = issuers.Any(x =>
                NamesCompatible(merchantKey, x.IssuerName) ||
                NamesCompatible(merchantKey, x.CommercialName));
            issuerMatches[merchantKey] = result;
            return result;
        }
    }

    internal static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    internal static bool NamesCompatible(
        string left,
        string right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
            return true;

        if (Math.Min(left.Length, right.Length) < 5)
            return false;

        return left.Contains(right, StringComparison.Ordinal) ||
               right.Contains(left, StringComparison.Ordinal);
    }

    private static BankVoucherReviewItemResponse Map(
        BankVoucher voucher,
        VoucherAnalysis analysis) =>
        new(
            voucher.Id,
            voucher.Bank,
            voucher.MerchantName,
            voucher.Amount,
            voucher.Currency,
            voucher.TransactionDate,
            voucher.CardBrand,
            voucher.CardLastFour,
            voucher.AuthorizationNumber,
            voucher.ReferenceNumber,
            voucher.Status,
            voucher.MatchedElectronicDocumentId,
            analysis.IsKnownMerchant,
            analysis.Candidates,
            voucher.CreatedAtUtc,
            voucher.UpdatedAtUtc);

    private sealed record VoucherAnalysis(
        bool IsKnownMerchant,
        IReadOnlyList<BankVoucherMatchCandidateResponse> Candidates);
}
