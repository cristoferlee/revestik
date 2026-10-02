using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.Purchases;

public sealed class PurchaseService(
    RevestikDbContext dbContext)
    : IPurchaseService
{
    public async Task<PurchaseResponse> CreateAsync(
        PurchaseCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            createdByUserId,
            cancellationToken);

        var supplierIsActive = await dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(
                supplier =>
                    supplier.Id == request.SupplierId &&
                    supplier.IsActive,
                cancellationToken);

        if (!supplierIsActive)
        {
            throw new InvalidPurchaseOperationException(
                "The selected supplier does not exist or is inactive.");
        }

        var productIds = request.Lines
            .Select(line => line.ProductId)
            .Distinct()
            .ToArray();

        if (productIds.Length != request.Lines.Count)
        {
            throw new InvalidPurchaseOperationException(
                "A product cannot appear more than once in the same purchase.");
        }

        var validProductCount = await dbContext.Products
            .AsNoTracking()
            .CountAsync(
                product =>
                    productIds.Contains(product.Id) &&
                    !product.IsDeleted &&
                    !product.IsArchived,
                cancellationToken);

        if (validProductCount != productIds.Length)
        {
            throw new InvalidPurchaseOperationException(
                "One or more selected products do not exist or are inactive.");
        }

        var purchaseDate = request.PurchaseDate
            ?? throw new InvalidPurchaseOperationException(
                "Purchase date is required.");

        ValidatePaymentTerms(request);

        var total = CalculateTotal(request.Lines);

        if (request.PaymentType == PurchasePaymentType.Cash &&
            request.InitialPayment?.Amount != total)
        {
            throw new InvalidPurchaseOperationException(
                "A cash purchase must be fully paid when it is registered.");
        }

        if (request.InitialPayment is not null &&
            request.InitialPayment.Amount > total)
        {
            throw new InvalidPurchaseOperationException(
                "The initial payment cannot exceed the purchase total.");
        }

        var now = DateTime.UtcNow;

        var purchase = new Purchase
        {
            SupplierId = request.SupplierId,
            PurchaseDate = purchaseDate,
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            PaymentType = request.PaymentType,
            CreditTermDays = request.PaymentType ==
                PurchasePaymentType.Credit
                    ? request.CreditTermDays
                    : null,
            DueDate = request.PaymentType ==
                PurchasePaymentType.Credit
                    ? purchaseDate.AddDays(
                        request.CreditTermDays!.Value)
                    : null,
            Notes = (request.Notes ?? string.Empty).Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            Lines = request.Lines
                .Select(line => new PurchaseLine
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost
                })
                .ToList()
        };

        if (request.InitialPayment is not null)
        {
            purchase.Payments.Add(
                CreatePayment(
                    purchase,
                    request.InitialPayment,
                    createdByUserId,
                    now,
                    allowPurchaseExchangeRateFallback: true));
        }

        dbContext.Purchases.Add(purchase);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
                purchase.Id,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The created purchase could not be loaded.");
    }

    public async Task<PurchaseResponse?> RegisterPaymentAsync(
        int purchaseId,
        PurchasePaymentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            createdByUserId,
            cancellationToken);

        var purchase = await dbContext.Purchases
            .Include(item => item.Lines)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(
                item => item.Id == purchaseId,
                cancellationToken);

        if (purchase is null)
        {
            return null;
        }

        ValidatePaymentExchangeRate(
            purchase.Currency,
            request.ExchangeRate,
            allowPurchaseExchangeRateFallback: false);

        var total = CalculateTotal(purchase.Lines);

        var currentPaidTotal = Round(
            purchase.Payments
                .Where(payment =>
                    payment.Status ==
                    PurchasePaymentStatus.Active)
                .Sum(payment => payment.Amount));

        var outstanding = Round(
            total - currentPaidTotal);

        if (outstanding <= 0m)
        {
            throw new InvalidPurchaseOperationException(
                "The purchase has no outstanding balance.");
        }

        if (request.Amount > outstanding)
        {
            throw new InvalidPurchaseOperationException(
                "The payment amount cannot exceed the outstanding balance.");
        }

        dbContext.PurchasePayments.Add(
            CreatePayment(
                purchase,
                request,
                createdByUserId,
                DateTime.UtcNow,
                allowPurchaseExchangeRateFallback: false));

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            purchase.Id,
            cancellationToken);
    }

    public async Task<PurchaseResponse?> VoidPaymentAsync(
        int purchaseId,
        int paymentId,
        VoidPurchasePaymentRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            voidedByUserId,
            cancellationToken);

        var payment = await dbContext.PurchasePayments
            .SingleOrDefaultAsync(
                item =>
                    item.Id == paymentId &&
                    item.PurchaseId == purchaseId,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.Status != PurchasePaymentStatus.Active)
        {
            throw new InvalidPurchaseOperationException(
                "Only active purchase payments can be voided.");
        }

        var reason = (request.Reason ?? string.Empty).Trim();

        if (reason.Length < 3)
        {
            throw new InvalidPurchaseOperationException(
                "The void reason must contain at least 3 characters.");
        }

        payment.Status =
            PurchasePaymentStatus.Voided;
        payment.VoidedByUserId =
            voidedByUserId;
        payment.VoidedAtUtc =
            DateTime.UtcNow;
        payment.VoidReason =
            reason;

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PurchasePaymentConcurrencyException();
        }

        return await GetByIdAsync(
            purchaseId,
            cancellationToken);
    }

    public async Task<PurchaseResponse?> GetByIdAsync(
        int purchaseId,
        CancellationToken cancellationToken)
    {
        var purchase = await dbContext.Purchases
            .AsNoTracking()
            .Where(item => item.Id == purchaseId)
            .Select(item => new
            {
                item.Id,
                item.SupplierId,
                SupplierName =
                    item.Supplier.Name,
                item.PurchaseDate,
                item.Currency,
                item.ExchangeRate,
                item.PaymentType,
                item.CreditTermDays,
                item.DueDate,
                item.Notes,
                item.CreatedByUserId,
                CreatedByDisplayName =
                    item.CreatedByUser.DisplayName,
                item.CreatedAtUtc,
                Lines = item.Lines
                    .OrderBy(line => line.Id)
                    .Select(line =>
                        new PurchaseLineResponse(
                            line.Id,
                            line.ProductId,
                            line.Product.Name,
                            line.Product.InventoryUnit.Symbol,
                            line.Quantity,
                            line.UnitCost))
                    .ToList(),
                Payments = item.Payments
                    .OrderByDescending(payment =>
                        payment.PaidAtUtc)
                    .ThenByDescending(payment =>
                        payment.Id)
                    .Select(payment =>
                        new PurchasePaymentResponse(
                            payment.Id,
                            payment.Amount,
                            payment.PaymentMethod,
                            payment.PaidAtUtc,
                            payment.ExchangeRate,
                            payment.Reference,
                            payment.Notes,
                            payment.Status,
                            payment.CreatedByUserId,
                            payment.CreatedByUser.DisplayName,
                            payment.CreatedAtUtc,
                            payment.VoidedByUser != null
                                ? payment.VoidedByUser.DisplayName
                                : string.Empty,
                            payment.VoidedAtUtc,
                            payment.VoidReason))
                    .ToList()
            })
            .SingleOrDefaultAsync(
                cancellationToken);

        if (purchase is null)
        {
            return null;
        }

        var total = Round(
            purchase.Lines.Sum(line =>
                line.Quantity * line.UnitCost));

        var paidTotal = Round(
            purchase.Payments
                .Where(payment =>
                    payment.Status ==
                    PurchasePaymentStatus.Active)
                .Sum(payment =>
                    payment.Amount));

        var outstanding = Math.Max(
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
            purchase.DueDate.HasValue &&
            purchase.DueDate.Value <
                DateOnly.FromDateTime(
                    DateTime.UtcNow);

        return new PurchaseResponse(
            purchase.Id,
            purchase.SupplierId,
            purchase.SupplierName,
            purchase.PurchaseDate,
            purchase.Currency,
            purchase.ExchangeRate,
            purchase.PaymentType,
            purchase.CreditTermDays,
            purchase.DueDate,
            purchase.Notes,
            total,
            paidTotal,
            outstanding,
            balanceStatus,
            isOverdue,
            purchase.CreatedByUserId,
            purchase.CreatedByDisplayName,
            purchase.CreatedAtUtc,
            purchase.Lines,
            purchase.Payments);
    }

    private async Task EnsureActiveUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidPurchaseOperationException(
                "The authenticated user identifier is required.");
        }

        var active = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Id == userId &&
                    user.IsActive,
                cancellationToken);

        if (!active)
        {
            throw new InvalidPurchaseOperationException(
                "The authenticated user does not exist or is inactive.");
        }
    }

    private static void ValidatePaymentTerms(
        PurchaseCreateRequest request)
    {
        if (!Enum.IsDefined(request.PaymentType))
        {
            throw new InvalidPurchaseOperationException(
                "The purchase payment type is invalid.");
        }

        if (request.PaymentType ==
            PurchasePaymentType.Cash)
        {
            if (request.CreditTermDays.HasValue)
            {
                throw new InvalidPurchaseOperationException(
                    "Cash purchases cannot have credit terms.");
            }

            if (request.InitialPayment is null)
            {
                throw new InvalidPurchaseOperationException(
                    "Cash purchases require an initial payment.");
            }
        }
        else
        {
            if (request.CreditTermDays is null or <= 0)
            {
                throw new InvalidPurchaseOperationException(
                    "Credit purchases require a positive credit term.");
            }
        }

        if (request.InitialPayment is not null)
        {
            ValidatePaymentExchangeRate(
                request.Currency,
                request.InitialPayment.ExchangeRate,
                allowPurchaseExchangeRateFallback: true);
        }
    }

    private static PurchasePayment CreatePayment(
        Purchase purchase,
        PurchasePaymentRequest request,
        string createdByUserId,
        DateTime createdAtUtc,
        bool allowPurchaseExchangeRateFallback)
    {
        ValidatePaymentExchangeRate(
            purchase.Currency,
            request.ExchangeRate,
            allowPurchaseExchangeRateFallback);

        var paymentExchangeRate =
            purchase.Currency == PurchaseCurrency.USD
                ? request.ExchangeRate ??
                  purchase.ExchangeRate
                : null;

        return new PurchasePayment
        {
            PurchaseId = purchase.Id,
            Purchase = purchase,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaidAtUtc = request.PaidAtUtc,
            ExchangeRate = paymentExchangeRate,
            Reference =
                (request.Reference ?? string.Empty).Trim(),
            Notes =
                (request.Notes ?? string.Empty).Trim(),
            Status =
                PurchasePaymentStatus.Active,
            CreatedByUserId =
                createdByUserId,
            CreatedAtUtc =
                createdAtUtc
        };
    }

    private static void ValidatePaymentExchangeRate(
        PurchaseCurrency currency,
        decimal? paymentExchangeRate,
        bool allowPurchaseExchangeRateFallback)
    {
        if (currency == PurchaseCurrency.CRC)
        {
            if (paymentExchangeRate.HasValue)
            {
                throw new InvalidPurchaseOperationException(
                    "CRC payments must not register an exchange rate.");
            }

            return;
        }

        if (!allowPurchaseExchangeRateFallback &&
            paymentExchangeRate is null or <= 0m)
        {
            throw new InvalidPurchaseOperationException(
                "USD payments require the payment-date exchange rate.");
        }

        if (paymentExchangeRate is <= 0m)
        {
            throw new InvalidPurchaseOperationException(
                "The payment exchange rate must be greater than zero.");
        }
    }

    private static decimal CalculateTotal(
        IEnumerable<PurchaseLine> lines) =>
        Round(lines.Sum(line =>
            line.Quantity * line.UnitCost));

    private static decimal CalculateTotal(
        IEnumerable<PurchaseLineRequest> lines) =>
        Round(lines.Sum(line =>
            line.Quantity * line.UnitCost));

    private static decimal Round(decimal value) =>
        decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
}