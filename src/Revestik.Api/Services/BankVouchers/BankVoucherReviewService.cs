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

    public async Task<IReadOnlyList<BankVoucherReviewItemResponse>> GetAsync(
        BankVoucherListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vouchersQuery = dbContext.BankVouchers.AsQueryable();

        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            vouchersQuery = vouchersQuery.Where(x =>
                x.TransactionDate >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(-6));

            vouchersQuery = vouchersQuery.Where(x =>
                x.TransactionDate < until);
        }

        var vouchers = await vouchersQuery
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var results = new List<BankVoucherReviewItemResponse>(vouchers.Count);

        foreach (var voucher in vouchers)
        {
            var analysis = await AnalyzeAsync(voucher, cancellationToken);

            if (voucher.Status == BankVoucherStatus.Accepted &&
                analysis.Candidates.Count > 0)
            {
                voucher.Status = BankVoucherStatus.NeedsReview;
                voucher.UpdatedAtUtc = DateTime.UtcNow;
            }

            results.Add(Map(voucher, analysis));
        }

        if (dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(cancellationToken);

        return results;
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

    private async Task<VoucherAnalysis> AnalyzeAsync(
        BankVoucher voucher,
        CancellationToken cancellationToken)
    {
        var merchantKey = NormalizeName(voucher.MerchantName);

        var suppliers = await dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Name,
                x.CommercialName
            })
            .ToListAsync(cancellationToken);

        var knownSupplier = suppliers.Any(x =>
            NamesCompatible(
                merchantKey,
                NormalizeName(x.Name)) ||
            NamesCompatible(
                merchantKey,
                NormalizeName(x.CommercialName)));

        var from = voucher.TransactionDate - MatchWindow;
        var until = voucher.TransactionDate + MatchWindow;

        var documentCandidates = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x =>
                x.TotalDocument == voucher.Amount &&
                x.CurrencyCode == voucher.Currency &&
                x.FechaEmision >= from &&
                x.FechaEmision <= until)
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

        var compatible = documentCandidates
            .Where(x =>
                NamesCompatible(
                    merchantKey,
                    NormalizeName(x.IssuerName)) ||
                NamesCompatible(
                    merchantKey,
                    NormalizeName(x.IssuerCommercialName)))
            .OrderBy(x => Math.Abs(
                (x.FechaEmision - voucher.TransactionDate).TotalMinutes))
            .Select(x => new BankVoucherMatchCandidateResponse(
                x.Id,
                x.IssuerName,
                string.IsNullOrWhiteSpace(x.IssuerCommercialName)
                    ? null
                    : x.IssuerCommercialName,
                x.FechaEmision,
                x.TotalDocument,
                x.CurrencyCode,
                x.AccountingNature,
                x.CategoryName))
            .ToList();

        var knownIssuer = compatible.Count > 0;

        if (!knownIssuer)
        {
            knownIssuer = await dbContext.ElectronicDocuments
                .AsNoTracking()
                .Select(x => new
                {
                    x.IssuerName,
                    x.IssuerCommercialName
                })
                .ToListAsync(cancellationToken)
                .ContinueWith(
                    task => task.Result.Any(x =>
                        NamesCompatible(
                            merchantKey,
                            NormalizeName(x.IssuerName)) ||
                        NamesCompatible(
                            merchantKey,
                            NormalizeName(x.IssuerCommercialName))),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        return new VoucherAnalysis(
            knownSupplier || knownIssuer,
            compatible);
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
