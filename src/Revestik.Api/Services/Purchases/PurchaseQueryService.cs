using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.Common;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.Purchases;

public sealed class PurchaseQueryService(
    RevestikDbContext dbContext)
    : IPurchaseQueryService
{
    public async Task<PaginatedResponse<PurchaseListItemResponse>>
        GetPageAsync(
            PurchaseListRequest request,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var query = BuildProjection();

        if (request.SupplierId.HasValue)
        {
            query = query.Where(item =>
                item.SupplierId == request.SupplierId.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(item =>
                item.PurchaseDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(item =>
                item.PurchaseDate <= request.DateTo.Value);
        }

        if (request.Currency.HasValue)
        {
            query = query.Where(item =>
                item.Currency == request.Currency.Value);
        }

        if (request.PaymentType.HasValue)
        {
            query = query.Where(item =>
                item.PaymentType == request.PaymentType.Value);
        }

        if (request.BalanceStatus.HasValue)
        {
            query = request.BalanceStatus.Value switch
            {
                PurchaseBalanceStatus.Pending =>
                    query.Where(item =>
                        item.PaidTotal == 0m &&
                        item.Total > 0m),

                PurchaseBalanceStatus.PartiallyPaid =>
                    query.Where(item =>
                        item.PaidTotal > 0m &&
                        item.PaidTotal < item.Total),

                PurchaseBalanceStatus.Paid =>
                    query.Where(item =>
                        item.PaidTotal >= item.Total),

                _ => query
            };
        }

        if (request.OverdueOnly == true)
        {
            query = query.Where(item =>
                item.DueDate.HasValue &&
                item.DueDate.Value < today &&
                item.PaidTotal < item.Total);
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var skip =
            ((long)request.Page - 1) *
            request.PageSize;

        IReadOnlyList<PurchaseListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            var rawItems = await query
                .OrderByDescending(item =>
                    item.PurchaseDate)
                .ThenByDescending(item =>
                    item.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            items = rawItems
                .Select(item =>
                    MapListItem(item, today))
                .ToList();
        }

        return new PaginatedResponse<PurchaseListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<PurchaseApSummaryResponse>
        GetApSummaryAsync(
            CancellationToken cancellationToken)
    {
        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var rawItems = await BuildProjection()
            .Where(item =>
                item.PaidTotal < item.Total)
            .ToListAsync(cancellationToken);

        var currencies = rawItems
            .GroupBy(item => item.Currency)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var overdue = group.Where(item =>
                    item.DueDate.HasValue &&
                    item.DueDate.Value < today);

                return new PurchaseCurrencyApSummaryResponse(
                    group.Key,
                    group.Count(),
                    Round(group.Sum(item =>
                        Math.Max(
                            0m,
                            item.Total - item.PaidTotal))),
                    overdue.Count(),
                    Round(overdue.Sum(item =>
                        Math.Max(
                            0m,
                            item.Total - item.PaidTotal))));
            })
            .ToList();

        return new PurchaseApSummaryResponse(currencies);
    }

    public async Task<PurchaseDueAlertResponse>
        GetDueAlertsAsync(
            PurchaseAlertRequest request,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ShortWindowDays <= 0 ||
            request.LongWindowDays < request.ShortWindowDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The alert windows are invalid.");
        }

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var lastDate =
            today.AddDays(request.LongWindowDays);

        var rawItems = await BuildProjection()
            .Where(item =>
                item.PaymentType ==
                    PurchasePaymentType.Credit &&
                item.DueDate.HasValue &&
                item.DueDate.Value <= lastDate &&
                item.PaidTotal < item.Total)
            .OrderBy(item =>
                item.DueDate)
            .ThenBy(item =>
                item.Id)
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item =>
            {
                var dueDate = item.DueDate!.Value;

                var daysUntilDue =
                    dueDate.DayNumber -
                    today.DayNumber;

                var alertType =
                    daysUntilDue < 0
                        ? PurchaseDueAlertType.Overdue
                        : daysUntilDue == 0
                            ? PurchaseDueAlertType.DueToday
                            : daysUntilDue <=
                                request.ShortWindowDays
                                ? PurchaseDueAlertType
                                    .DueWithinShortWindow
                                : PurchaseDueAlertType
                                    .DueWithinLongWindow;

                return new PurchaseDueAlertItemResponse(
                    item.Id,
                    item.SupplierId,
                    item.SupplierName,
                    item.Currency,
                    Round(
                        Math.Max(
                            0m,
                            item.Total - item.PaidTotal)),
                    dueDate,
                    daysUntilDue,
                    alertType);
            })
            .ToList();

        return new PurchaseDueAlertResponse(
            request.ShortWindowDays,
            request.LongWindowDays,
            items);
    }

    public async Task<SupplierPurchaseHistoryResponse?>
        GetSupplierHistoryAsync(
            int supplierId,
            SupplierPurchaseHistoryRequest request,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .Where(item => item.Id == supplierId)
            .Select(item => new
            {
                item.Id,
                item.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (supplier is null)
        {
            return null;
        }

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var supplierPurchases = BuildProjection()
            .Where(item =>
                item.SupplierId == supplierId);

        var purchaseCount =
            await supplierPurchases.CountAsync(
                cancellationToken);

        var lastPurchaseDate =
            await supplierPurchases
                .MaxAsync(
                    item =>
                        (DateOnly?)item.PurchaseDate,
                    cancellationToken);

        var summaryRows =
            await supplierPurchases
                .Select(item => new
                {
                    item.Currency,
                    item.Total,
                    item.PaidTotal
                })
                .ToListAsync(cancellationToken);

        var currencies = summaryRows
            .GroupBy(item => item.Currency)
            .OrderBy(group => group.Key)
            .Select(group =>
                new SupplierPurchaseCurrencySummaryResponse(
                    group.Key,
                    Round(group.Sum(item =>
                        item.Total)),
                    Round(group.Sum(item =>
                        Math.Max(
                            0m,
                            item.Total - item.PaidTotal)))))
            .ToList();

        var skip =
            ((long)request.Page - 1) *
            request.PageSize;

        IReadOnlyList<PurchaseListItemResponse> items;

        if (skip >= purchaseCount)
        {
            items = [];
        }
        else
        {
            var rawItems = await supplierPurchases
                .OrderByDescending(item =>
                    item.PurchaseDate)
                .ThenByDescending(item =>
                    item.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            items = rawItems
                .Select(item =>
                    MapListItem(item, today))
                .ToList();
        }

        return new SupplierPurchaseHistoryResponse(
            supplier.Id,
            supplier.Name,
            lastPurchaseDate,
            purchaseCount,
            currencies,
            new PaginatedResponse<PurchaseListItemResponse>(
                items,
                request.Page,
                request.PageSize,
                purchaseCount));
    }

    private IQueryable<PurchaseProjection>
        BuildProjection()
    {
        return dbContext.Purchases
            .AsNoTracking()
            .Select(purchase =>
                new PurchaseProjection
                {
                    Id = purchase.Id,
                    PurchaseDate =
                        purchase.PurchaseDate,
                    SupplierId =
                        purchase.SupplierId,
                    SupplierName =
                        purchase.Supplier.Name,
                    Currency =
                        purchase.Currency,
                    PaymentType =
                        purchase.PaymentType,
                    DueDate =
                        purchase.DueDate,
                    Total =
                        purchase.Lines
                            .Sum(line =>
                                (decimal?)
                                (line.Quantity *
                                 line.UnitCost))
                        ?? 0m,
                    PaidTotal =
                        purchase.Payments
                            .Where(payment =>
                                payment.Status ==
                                PurchasePaymentStatus.Active)
                            .Sum(payment =>
                                (decimal?)payment.Amount)
                        ?? 0m
                });
    }

    private static PurchaseListItemResponse MapListItem(
        PurchaseProjection item,
        DateOnly today)
    {
        var total = Round(item.Total);
        var paidTotal = Round(item.PaidTotal);
        var outstanding =
            Math.Max(
                0m,
                Round(total - paidTotal));

        var balanceStatus =
            outstanding == 0m
                ? PurchaseBalanceStatus.Paid
                : paidTotal > 0m
                    ? PurchaseBalanceStatus.PartiallyPaid
                    : PurchaseBalanceStatus.Pending;

        var isOverdue =
            outstanding > 0m &&
            item.DueDate.HasValue &&
            item.DueDate.Value < today;

        return new PurchaseListItemResponse(
            item.Id,
            item.PurchaseDate,
            item.SupplierId,
            item.SupplierName,
            item.Currency,
            item.PaymentType,
            total,
            paidTotal,
            outstanding,
            balanceStatus,
            item.DueDate,
            isOverdue);
    }

    private static decimal Round(decimal value) =>
        decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);

    private sealed class PurchaseProjection
    {
        public int Id { get; init; }
        public DateOnly PurchaseDate { get; init; }
        public int SupplierId { get; init; }
        public string SupplierName { get; init; } =
            string.Empty;
        public PurchaseCurrency Currency { get; init; }
        public PurchasePaymentType PaymentType { get; init; }
        public DateOnly? DueDate { get; init; }
        public decimal Total { get; init; }
        public decimal PaidTotal { get; init; }
    }
}