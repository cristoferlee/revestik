using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Sales;
using QuotationDiscountType = Revestik.Shared.Quotations.DiscountType;
using QuotationCurrency = Revestik.Shared.Quotations.Currency;
using QuotationChargeType = Revestik.Shared.Quotations.QuotationChargeType;
using QuotationStatus = Revestik.Shared.Quotations.QuotationStatus;

namespace Revestik.Api.Services.Sales;

public sealed class SaleService(
    RevestikDbContext dbContext,
    ISaleNumberGenerator saleNumberGenerator)
    : ISaleService
{

    public async Task<PaginatedResponse<SaleListItemResponse>>
        GetPageAsync(
            SaleListRequest request,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.Sales
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(sale =>
                (sale.SaleNumber != null &&
                 sale.SaleNumber.Contains(search)) ||
                sale.Customer.Name.Contains(search) ||
                (sale.SourceQuotation != null &&
                 sale.SourceQuotation.QuotationNumber.Contains(search)));
        }

        if (request.DateFromUtc.HasValue)
        {
            query = query.Where(sale =>
                sale.IssuedAtUtc >= request.DateFromUtc.Value);
        }

        if (request.DateToUtc.HasValue)
        {
            query = query.Where(sale =>
                sale.IssuedAtUtc <= request.DateToUtc.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(sale =>
                sale.Status == request.Status.Value);
        }

        if (request.Currency.HasValue)
        {
            query = query.Where(sale =>
                sale.Currency == request.Currency.Value);
        }

        var projected = query.Select(sale => new
        {
            Sale = sale,
            PaidTotal = sale.Payments
                .Where(payment =>
                    payment.Status == SalePaymentStatus.Active)
                .Sum(payment => (decimal?)payment.Amount) ?? 0m
        });

        var rawItems = await projected
            .OrderByDescending(item =>
                item.Sale.IssuedAtUtc ??
                item.Sale.CreatedAtUtc)
            .ThenByDescending(item =>
                item.Sale.Id)
            .Select(item => new
                {
                    item.Sale.Id,
                    SaleNumber =
                        item.Sale.SaleNumber ?? string.Empty,
                    SourceQuotationNumber =
                        item.Sale.SourceQuotation != null
                            ? item.Sale.SourceQuotation.QuotationNumber
                            : string.Empty,
                    item.Sale.IssuedAtUtc,
                    CustomerName =
                        item.Sale.Status != SaleStatus.Draft &&
                        item.Sale.CustomerNameSnapshot != string.Empty
                            ? item.Sale.CustomerNameSnapshot
                            : item.Sale.Customer.Name,
                    item.Sale.Currency,
                    item.Sale.Status,
                    item.PaidTotal,
                    item.Sale.GeneralDiscountType,
                    item.Sale.GeneralDiscountValue,
                    Lines = item.Sale.Lines.Select(line =>
                        new SaleLineRequest
                        {
                            ProductId = line.ProductId,
                            CabysCode =
                                line.CabysCode ?? string.Empty,
                            Description = line.Description,
                            Unit = line.Unit,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            DiscountType = line.DiscountType,
                            DiscountValue = line.DiscountValue,
                            TaxRate = line.TaxRate
                        }).ToList(),
                    Charges = item.Sale.Charges.Select(charge =>
                        new SaleChargeRequest
                        {
                            Type = charge.Type,
                            Description = charge.Description,
                            Amount = charge.Amount
                        }).ToList()
                })
                .ToListAsync(cancellationToken);

        var mappedItems = rawItems
            .Select(item =>
            {
                var total = SaleCalculator.Calculate(
                    item.Lines,
                    item.Charges,
                    item.GeneralDiscountType,
                    item.GeneralDiscountValue).Total;

                var paidTotal = Round(item.PaidTotal);
                var outstanding =
                    Math.Max(
                        0m,
                        Round(total - paidTotal));

                return new SaleListItemResponse
                {
                    Id = item.Id,
                    SaleNumber = item.SaleNumber,
                    SourceQuotationNumber =
                        item.SourceQuotationNumber,
                    IssuedAtUtc = item.IssuedAtUtc,
                    CustomerName = item.CustomerName,
                    Currency = item.Currency,
                    Total = total,
                    PaidTotal = paidTotal,
                    OutstandingAmount = outstanding,
                    Status = item.Status,
                    BalanceStatus =
                        CalculateBalanceStatus(
                            total,
                            paidTotal)
                };
            });

        if (request.BalanceStatus.HasValue)
        {
            mappedItems = mappedItems.Where(item =>
                item.BalanceStatus ==
                request.BalanceStatus.Value);
        }

        var filteredItems = mappedItems.ToList();
        var totalCount = filteredItems.Count;

        var skip =
            ((long)request.Page - 1) *
            request.PageSize;

        IReadOnlyList<SaleListItemResponse> items =
            skip >= totalCount
                ? []
                : filteredItems
                    .Skip((int)skip)
                    .Take(request.PageSize)
                    .ToList();

        return new PaginatedResponse<SaleListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<SaleSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Where(sale =>
                sale.Status == SaleStatus.Issued)
            .Select(sale => new
            {
                sale.Currency,
                sale.GeneralDiscountType,
                sale.GeneralDiscountValue,
                Lines = sale.Lines.Select(line =>
                    new SaleLineRequest
                    {
                        ProductId = line.ProductId,
                        CabysCode =
                            line.CabysCode ?? string.Empty,
                        Description = line.Description,
                        Unit = line.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        DiscountType = line.DiscountType,
                        DiscountValue = line.DiscountValue,
                        TaxRate = line.TaxRate
                    }).ToList(),
                Charges = sale.Charges.Select(charge =>
                    new SaleChargeRequest
                    {
                        Type = charge.Type,
                        Description = charge.Description,
                        Amount = charge.Amount
                    }).ToList(),
                PaidTotal = sale.Payments
                    .Where(payment =>
                        payment.Status ==
                        SalePaymentStatus.Active)
                    .Sum(payment =>
                        (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var currencies = sales
            .Select(sale =>
            {
                var total = SaleCalculator.Calculate(
                    sale.Lines,
                    sale.Charges,
                    sale.GeneralDiscountType,
                    sale.GeneralDiscountValue).Total;

                var paid = Round(sale.PaidTotal);

                return new
                {
                    sale.Currency,
                    Total = total,
                    Paid = paid,
                    Outstanding =
                        Math.Max(
                            0m,
                            Round(total - paid))
                };
            })
            .GroupBy(item => item.Currency)
            .OrderBy(group => group.Key)
            .Select(group =>
                new SaleCurrencySummaryResponse
                {
                    Currency = group.Key,
                    SoldTotal = Round(
                        group.Sum(item => item.Total)),
                    CollectedTotal = Round(
                        group.Sum(item => item.Paid)),
                    OutstandingTotal = Round(
                        group.Sum(item =>
                            item.Outstanding)),
                    SaleCount = group.Count()
                })
            .ToList();

        return new SaleSummaryResponse
        {
            Currencies = currencies
        };
    }

    public async Task<SaleResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var sale = await dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.Id == id)
            .Select(sale => new SaleResponse
            {
                Id = sale.Id,
                SaleNumber = sale.SaleNumber ?? string.Empty,
                SourceQuotationId = sale.SourceQuotationId,
                SourceQuotationNumber =
                    sale.SourceQuotation != null
                        ? sale.SourceQuotation.QuotationNumber
                        : string.Empty,
                CustomerId = sale.CustomerId,
                CustomerName =
                    sale.Status != SaleStatus.Draft &&
                    sale.CustomerNameSnapshot != string.Empty
                        ? sale.CustomerNameSnapshot
                        : sale.Customer.Name,
                CustomerIdentificationNumber =
                    sale.Status != SaleStatus.Draft &&
                    sale.CustomerIdentificationNumberSnapshot != string.Empty
                        ? sale.CustomerIdentificationNumberSnapshot
                        : sale.Customer.IdentificationNumber,
                CustomerEmail =
                    sale.Status != SaleStatus.Draft &&
                    sale.CustomerEmailSnapshot != string.Empty
                        ? sale.CustomerEmailSnapshot
                        : sale.Customer.Email,
                CustomerPhoneNumber =
                    sale.Status != SaleStatus.Draft &&
                    sale.CustomerPhoneNumberSnapshot != string.Empty
                        ? sale.CustomerPhoneNumberSnapshot
                        : sale.Customer.PhoneNumber,
                Currency = sale.Currency,
                Status = sale.Status,
                GeneralDiscountType = sale.GeneralDiscountType,
                GeneralDiscountValue = sale.GeneralDiscountValue,
                IssuedAtUtc = sale.IssuedAtUtc,
                VoidedAtUtc = sale.VoidedAtUtc,
                VoidReason = sale.VoidReason,
                ReplacesSaleId = sale.ReplacesSaleId,
                ReplacementSaleId =
                    sale.ReplacementSale != null
                        ? sale.ReplacementSale.Id
                        : null,
                Observations = sale.Observations,
                CreatedByUserId = sale.CreatedByUserId,
                CreatedByDisplayName =
                    sale.CreatedByUser.DisplayName,
                IssuedByDisplayName =
                    sale.IssuedByUser != null
                        ? sale.IssuedByUser.DisplayName
                        : string.Empty,
                VoidedByDisplayName =
                    sale.VoidedByUser != null
                        ? sale.VoidedByUser.DisplayName
                        : string.Empty,
                CreatedAtUtc = sale.CreatedAtUtc,
                UpdatedAtUtc = sale.UpdatedAtUtc,

                Lines = sale.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new SaleLineResponse
                    {
                        Id = line.Id,
                        ProductId = line.ProductId,
                        CabysCode =
                            line.CabysCode ?? string.Empty,
                        Description = line.Description,
                        Unit = line.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        DiscountType = line.DiscountType,
                        DiscountValue = line.DiscountValue,
                        TaxRate = line.TaxRate
                    })
                    .ToList(),

                Charges = sale.Charges
                    .OrderBy(charge => charge.Id)
                    .Select(charge =>
                        new SaleChargeResponse
                        {
                            Id = charge.Id,
                            Type = charge.Type,
                            Description = charge.Description,
                            Amount = charge.Amount
                        })
                    .ToList(),

                Payments = sale.Payments
                    .OrderByDescending(payment => payment.PaidAtUtc)
                    .ThenByDescending(payment => payment.Id)
                    .Select(payment => new SalePaymentResponse
                    {
                        Id = payment.Id,
                        Amount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod,
                        PaidAtUtc = payment.PaidAtUtc,
                        Reference = payment.Reference,
                        Notes = payment.Notes,
                        Status = payment.Status,
                        CreatedByUserId = payment.CreatedByUserId,
                        CreatedByDisplayName =
                            payment.CreatedByUser.DisplayName,
                        CreatedAtUtc = payment.CreatedAtUtc,
                        VoidedByDisplayName =
                            payment.VoidedByUser != null
                                ? payment.VoidedByUser.DisplayName
                                : string.Empty,
                        VoidedAtUtc = payment.VoidedAtUtc,
                        VoidReason = payment.VoidReason
                    })
                    .ToList(),

                PaidTotal = sale.Payments
                    .Where(payment =>
                        payment.Status ==
                        SalePaymentStatus.Active)
                    .Sum(payment =>
                        (decimal?)payment.Amount) ?? 0m
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sale is null)
        {
            return null;
        }

        PopulateCalculatedTotals(sale);

        sale.OutstandingAmount =
            Math.Max(
                0m,
                Round(sale.Total - sale.PaidTotal));

        sale.BalanceStatus =
            CalculateBalanceStatus(
                sale.Total,
                sale.PaidTotal);

        sale.NextPaymentDueAtUtc =
            CalculateNextPaymentDueAtUtc(sale);

        return sale;
    }

    public async Task<SaleResponse> CreateAsync(
        SaleUpsertRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException(
                "The creator user ID is required.",
                nameof(createdByUserId));
        }

        await EnsureActiveUserAsync(
            createdByUserId,
            "The creator user does not exist or is inactive.",
            cancellationToken);

        await ValidateRequestBusinessRulesAsync(
            request,
            cancellationToken);

        _ = SaleCalculator.Calculate(
            request.Lines,
            request.Charges,
            request.GeneralDiscountType,
            request.GeneralDiscountValue);

        var sale = new Sale
        {
            SaleNumber = null,
            CustomerId = request.CustomerId,
            CreatedByUserId = createdByUserId,
            Currency = request.Currency,
            Status = SaleStatus.Draft,
            GeneralDiscountType = request.GeneralDiscountType,
            GeneralDiscountValue = request.GeneralDiscountValue,
            Observations = request.Observations.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        ApplyLinesAndCharges(sale, request);

        dbContext.Sales.Add(sale);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The sale could not be loaded after creation.");
    }

    public async Task<SaleResponse?> CreateFromQuotationAsync(
        int quotationId,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException(
                "The creator user ID is required.",
                nameof(createdByUserId));
        }

        await EnsureActiveUserAsync(
            createdByUserId,
            "The creator user does not exist or is inactive.",
            cancellationToken);

        var quotation = await dbContext.Quotations
            .AsNoTracking()
            .Include(quotation => quotation.Lines)
            .Include(quotation => quotation.Charges)
            .SingleOrDefaultAsync(
                quotation => quotation.Id == quotationId,
                cancellationToken);

        if (quotation is null)
        {
            return null;
        }

        if (quotation.Status != QuotationStatus.Issued)
        {
            throw new InvalidOperationException(
                "Only issued quotations can be converted to a sale.");
        }

        var alreadyConverted = await dbContext.Sales
            .AsNoTracking()
            .AnyAsync(
                sale =>
                    sale.SourceQuotationId == quotationId,
                cancellationToken);

        if (alreadyConverted)
        {
            throw new InvalidOperationException(
                "The quotation already has an associated sale.");
        }

        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer =>
                    customer.Id == quotation.CustomerId &&
                    customer.IsActive,
                cancellationToken);

        if (!customerExists)
        {
            throw new InvalidOperationException(
                "The customer does not exist or is inactive.");
        }

        var sale = new Sale
        {
            SaleNumber = null,
            SourceQuotationId = quotation.Id,
            CustomerId = quotation.CustomerId,
            CreatedByUserId = createdByUserId,
            Currency = MapCurrency(quotation.Currency),
            Status = SaleStatus.Draft,
            GeneralDiscountType = null,
            GeneralDiscountValue = 0m,
            Observations = quotation.Observations.Trim(),
            CreatedAtUtc = DateTime.UtcNow,

            Lines = quotation.Lines
                .OrderBy(line => line.Id)
                .Select(line => new SaleLine
                {
                    ProductId = line.ProductId,
                    CabysCode = line.CabysCode,
                    Description = line.Description,
                    Unit = line.Unit,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountType =
                        MapDiscountType(line.DiscountType),
                    DiscountValue = line.DiscountValue,
                    TaxRate = line.TaxRate
                })
                .ToList(),

            Charges = quotation.Charges
                .OrderBy(charge => charge.Id)
                .Select(charge => new SaleCharge
                {
                    Type = MapChargeType(charge.Type),
                    Description = charge.Description,
                    Amount = charge.Amount
                })
                .ToList()
        };

        dbContext.Sales.Add(sale);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The sale could not be loaded after quotation conversion.");
    }

    public async Task<SaleResponse?> UpdateDraftAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sale = await dbContext.Sales
            .Include(sale => sale.Lines)
            .Include(sale => sale.Charges)
            .SingleOrDefaultAsync(
                sale => sale.Id == id,
                cancellationToken);

        if (sale is null)
        {
            return null;
        }

        if (sale.Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft sales can be edited.");
        }

        await ApplyRequestAsync(
            sale,
            request,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken);
    }

    public async Task<SaleResponse?> IssueAsync(
        int id,
        SaleUpsertRequest request,
        string issuedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(issuedByUserId))
        {
            throw new ArgumentException(
                "The issuing user ID is required.",
                nameof(issuedByUserId));
        }

        var sale = await dbContext.Sales
            .Include(sale => sale.Lines)
            .Include(sale => sale.Charges)
            .SingleOrDefaultAsync(
                sale => sale.Id == id,
                cancellationToken);

        if (sale is null)
        {
            return null;
        }

        if (sale.Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft sales can be issued.");
        }

        await EnsureActiveUserAsync(
            issuedByUserId,
            "The issuing user does not exist or is inactive.",
            cancellationToken);

        var customer = await ApplyRequestAsync(
            sale,
            request,
            cancellationToken);

        sale.CustomerNameSnapshot = customer.Name;
        sale.CustomerIdentificationNumberSnapshot =
            customer.IdentificationNumber;
        sale.CustomerEmailSnapshot = customer.Email;
        sale.CustomerPhoneNumberSnapshot =
            customer.PhoneNumber;

        sale.SaleNumber =
            await saleNumberGenerator.GenerateAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        sale.Status = SaleStatus.Issued;
        sale.IssuedByUserId = issuedByUserId;
        sale.IssuedAtUtc = now;
        sale.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken);
    }

    public async Task<SaleResponse?> VoidAsync(
        int id,
        VoidSaleRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(voidedByUserId))
        {
            throw new ArgumentException(
                "The voiding user ID is required.",
                nameof(voidedByUserId));
        }

        var sale = await dbContext.Sales
            .Include(sale => sale.Payments)
            .SingleOrDefaultAsync(
                sale => sale.Id == id,
                cancellationToken);

        if (sale is null)
        {
            return null;
        }

        ValidateSaleCanBeVoided(sale);

        await EnsureActiveUserAsync(
            voidedByUserId,
            "The voiding user does not exist or is inactive.",
            cancellationToken);

        ApplyVoid(
            sale,
            request.Reason,
            voidedByUserId);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken);
    }

    public async Task<SaleResponse?> CreateReplacementAsync(
        int id,
        VoidSaleRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException(
                "The creator user ID is required.",
                nameof(createdByUserId));
        }

        var original = await dbContext.Sales
            .Include(sale => sale.Lines)
            .Include(sale => sale.Charges)
            .Include(sale => sale.Payments)
            .Include(sale => sale.ReplacementSale)
            .SingleOrDefaultAsync(
                sale => sale.Id == id,
                cancellationToken);

        if (original is null)
        {
            return null;
        }

        ValidateSaleCanBeVoided(original);

        if (original.ReplacementSale is not null)
        {
            throw new InvalidOperationException(
                "The sale already has a replacement.");
        }

        await EnsureActiveUserAsync(
            createdByUserId,
            "The creator user does not exist or is inactive.",
            cancellationToken);

        var now = DateTime.UtcNow;

        ApplyVoid(
            original,
            request.Reason,
            createdByUserId,
            now);

        var replacement = new Sale
        {
            SaleNumber = null,
            SourceQuotationId = null,
            CustomerId = original.CustomerId,
            CreatedByUserId = createdByUserId,
            ReplacesSaleId = original.Id,
            Currency = original.Currency,
            Status = SaleStatus.Draft,
            GeneralDiscountType =
                original.GeneralDiscountType,
            GeneralDiscountValue =
                original.GeneralDiscountValue,
            Observations = original.Observations,
            CreatedAtUtc = now,

            Lines = original.Lines
                .OrderBy(line => line.Id)
                .Select(line => new SaleLine
                {
                    ProductId = line.ProductId,
                    CabysCode = line.CabysCode,
                    Description = line.Description,
                    Unit = line.Unit,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountType = line.DiscountType,
                    DiscountValue = line.DiscountValue,
                    TaxRate = line.TaxRate
                })
                .ToList(),

            Charges = original.Charges
                .OrderBy(charge => charge.Id)
                .Select(charge => new SaleCharge
                {
                    Type = charge.Type,
                    Description = charge.Description,
                    Amount = charge.Amount
                })
                .ToList()
        };

        dbContext.Sales.Add(replacement);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            replacement.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The replacement sale could not be loaded after creation.");
    }


    public async Task<SaleResponse?> RegisterPaymentAsync(
        int id,
        SalePaymentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException(
                "The creator user ID is required.",
                nameof(createdByUserId));
        }

        var sale = await dbContext.Sales
            .Include(sale => sale.Lines)
            .Include(sale => sale.Charges)
            .Include(sale => sale.Payments)
            .SingleOrDefaultAsync(
                sale => sale.Id == id,
                cancellationToken);

        if (sale is null)
        {
            return null;
        }

        if (sale.Status != SaleStatus.Issued)
        {
            throw new InvalidOperationException(
                "Payments can only be registered for issued sales.");
        }

        await EnsureActiveUserAsync(
            createdByUserId,
            "The creator user does not exist or is inactive.",
            cancellationToken);

        var total = CalculateSaleTotal(sale);

        var currentPaidTotal = Round(
            sale.Payments
                .Where(payment =>
                    payment.Status ==
                    SalePaymentStatus.Active)
                .Sum(payment => payment.Amount));

        var outstandingAmount =
            Round(total - currentPaidTotal);

        if (request.Amount > outstandingAmount)
        {
            throw new InvalidOperationException(
                "The payment amount cannot exceed the outstanding balance.");
        }

        var payment = new SalePayment
        {
            SaleId = sale.Id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaidAtUtc = request.PaidAtUtc,
            Reference = request.Reference.Trim(),
            Notes = request.Notes.Trim(),
            Status = SalePaymentStatus.Active,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.SalePayments.Add(payment);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            sale.Id,
            cancellationToken);
    }

    public async Task<SaleResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidSalePaymentRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(voidedByUserId))
        {
            throw new ArgumentException(
                "The voiding user ID is required.",
                nameof(voidedByUserId));
        }

        var payment = await dbContext.SalePayments
            .Include(payment => payment.Sale)
            .SingleOrDefaultAsync(
                payment =>
                    payment.Id == paymentId &&
                    payment.SaleId == id,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.Status != SalePaymentStatus.Active)
        {
            throw new InvalidOperationException(
                "Only active payments can be voided.");
        }

        await EnsureActiveUserAsync(
            voidedByUserId,
            "The voiding user does not exist or is inactive.",
            cancellationToken);

        var reason = request.Reason.Trim();

        if (reason.Length < 3)
        {
            throw new ArgumentException(
                "The void reason must contain at least 3 characters.",
                nameof(request));
        }

        var now = DateTime.UtcNow;

        payment.Status = SalePaymentStatus.Voided;
        payment.VoidedByUserId = voidedByUserId;
        payment.VoidedAtUtc = now;
        payment.VoidReason = reason;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            id,
            cancellationToken);
    }

    private async Task<Customer> ApplyRequestAsync(
        Sale sale,
        SaleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var customer =
            await ValidateRequestBusinessRulesAsync(
                request,
                cancellationToken);

        _ = SaleCalculator.Calculate(
            request.Lines,
            request.Charges,
            request.GeneralDiscountType,
            request.GeneralDiscountValue);

        sale.CustomerId = request.CustomerId;
        sale.Currency = request.Currency;
        sale.GeneralDiscountType =
            request.GeneralDiscountType;
        sale.GeneralDiscountValue =
            request.GeneralDiscountValue;
        sale.Observations =
            request.Observations.Trim();
        sale.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.SaleLines.RemoveRange(
            sale.Lines);

        dbContext.SaleCharges.RemoveRange(
            sale.Charges);

        ApplyLinesAndCharges(sale, request);

        return customer;
    }

    private async Task<Customer>
        ValidateRequestBusinessRulesAsync(
            SaleUpsertRequest request,
            CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                customer =>
                    customer.Id == request.CustomerId &&
                    customer.IsActive,
                cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException(
                "The customer does not exist or is inactive.");
        }

        var productIds = request.Lines
            .Where(line => line.ProductId.HasValue)
            .Select(line => line.ProductId!.Value)
            .Distinct()
            .ToArray();

        if (productIds.Length > 0)
        {
            var existingProductIds =
                await dbContext.Products
                    .AsNoTracking()
                    .Where(product =>
                        productIds.Contains(product.Id) &&
                        !product.IsDeleted)
                    .Select(product => product.Id)
                    .ToListAsync(cancellationToken);

            if (existingProductIds.Count !=
                productIds.Length)
            {
                throw new InvalidOperationException(
                    "One or more selected products do not exist or are deleted.");
            }
        }

        return customer;
    }

    private static void ValidateSaleCanBeVoided(
        Sale sale)
    {
        if (sale.Status != SaleStatus.Issued)
        {
            throw new InvalidOperationException(
                "Only issued sales can be voided.");
        }

        var hasActivePayments = sale.Payments
            .Any(payment =>
                payment.Status ==
                SalePaymentStatus.Active);

        if (hasActivePayments)
        {
            throw new InvalidOperationException(
                "A sale with active payments cannot be voided.");
        }
    }

    private static void ApplyVoid(
        Sale sale,
        string reason,
        string voidedByUserId,
        DateTime? now = null)
    {
        var normalizedReason = reason.Trim();

        if (normalizedReason.Length < 3)
        {
            throw new ArgumentException(
                "The void reason must contain at least 3 characters.",
                nameof(reason));
        }

        var timestamp = now ?? DateTime.UtcNow;

        sale.Status = SaleStatus.Voided;
        sale.VoidedByUserId = voidedByUserId;
        sale.VoidedAtUtc = timestamp;
        sale.VoidReason = normalizedReason;
        sale.UpdatedAtUtc = timestamp;
    }

    private static void ApplyLinesAndCharges(
        Sale sale,
        SaleUpsertRequest request)
    {
        sale.Lines = request.Lines
            .Select(line => new SaleLine
            {
                ProductId = line.ProductId,
                CabysCode =
                    NormalizeCabysCode(
                        line.CabysCode),
                Description =
                    line.Description.Trim(),
                Unit = line.Unit.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountType =
                    line.DiscountType,
                DiscountValue =
                    line.DiscountValue,
                TaxRate = line.TaxRate
            })
            .ToList();

        sale.Charges = request.Charges
            .Select(charge => new SaleCharge
            {
                Type = charge.Type,
                Description =
                    charge.Description.Trim(),
                Amount = charge.Amount
            })
            .ToList();
    }

    private async Task EnsureActiveUserAsync(
        string userId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Id == userId &&
                    user.IsActive,
                cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                errorMessage);
        }
    }

    private static Currency MapCurrency(
        QuotationCurrency currency)
    {
        return currency switch
        {
            QuotationCurrency.CRC => Currency.CRC,
            QuotationCurrency.USD => Currency.USD,
            _ => throw new ArgumentOutOfRangeException(
                nameof(currency),
                "Unsupported quotation currency.")
        };
    }

    private static DiscountType? MapDiscountType(
        QuotationDiscountType? discountType)
    {
        return discountType switch
        {
            null => null,
            QuotationDiscountType.Percentage =>
                DiscountType.Percentage,
            QuotationDiscountType.FixedAmount =>
                DiscountType.FixedAmount,
            _ => throw new ArgumentOutOfRangeException(
                nameof(discountType),
                "Unsupported quotation discount type.")
        };
    }

    private static SaleChargeType MapChargeType(
        QuotationChargeType chargeType)
    {
        return chargeType switch
        {
            QuotationChargeType.Service =>
                SaleChargeType.Service,
            QuotationChargeType.Transport =>
                SaleChargeType.Transport,
            QuotationChargeType.Installation =>
                SaleChargeType.Installation,
            QuotationChargeType.Other =>
                SaleChargeType.Other,
            _ => throw new ArgumentOutOfRangeException(
                nameof(chargeType),
                "Unsupported quotation charge type.")
        };
    }

    private static void PopulateCalculatedTotals(
        SaleResponse sale)
    {
        var requests = sale.Lines
            .Select(line => new SaleLineRequest
            {
                ProductId = line.ProductId,
                CabysCode = line.CabysCode,
                Description = line.Description,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountType =
                    line.DiscountType,
                DiscountValue =
                    line.DiscountValue,
                TaxRate = line.TaxRate
            })
            .ToList();

        var chargeRequests = sale.Charges
            .Select(charge =>
                new SaleChargeRequest
                {
                    Type = charge.Type,
                    Description =
                        charge.Description,
                    Amount = charge.Amount
                })
            .ToList();

        var calculation =
            SaleCalculator.Calculate(
                requests,
                chargeRequests,
                sale.GeneralDiscountType,
                sale.GeneralDiscountValue);

        for (var index = 0;
             index < sale.Lines.Count;
             index++)
        {
            var line = sale.Lines[index];
            var lineCalculation =
                calculation.Lines[index];

            line.BaseAmount =
                lineCalculation.BaseAmount;
            line.LineDiscountAmount =
                lineCalculation.LineDiscountAmount;
            line.GeneralDiscountAmount =
                lineCalculation.GeneralDiscountAmount;
            line.TaxAmount =
                lineCalculation.TaxAmount;
            line.TotalAmount =
                lineCalculation.TotalAmount;
        }

        sale.Subtotal = calculation.Subtotal;
        sale.LineDiscountTotal =
            calculation.LineDiscountTotal;
        sale.GeneralDiscountTotal =
            calculation.GeneralDiscountTotal;
        sale.DiscountTotal =
            calculation.DiscountTotal;
        sale.TaxTotal = calculation.TaxTotal;
        sale.ChargeTotal =
            calculation.ChargeTotal;
        sale.Total = calculation.Total;
        sale.PaidTotal = Round(sale.PaidTotal);
    }


    private static decimal CalculateSaleTotal(
        Sale sale)
    {
        var lineRequests = sale.Lines
            .Select(line => new SaleLineRequest
            {
                ProductId = line.ProductId,
                CabysCode = line.CabysCode ?? string.Empty,
                Description = line.Description,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountType = line.DiscountType,
                DiscountValue = line.DiscountValue,
                TaxRate = line.TaxRate
            })
            .ToList();

        var chargeRequests = sale.Charges
            .Select(charge => new SaleChargeRequest
            {
                Type = charge.Type,
                Description = charge.Description,
                Amount = charge.Amount
            })
            .ToList();

        return SaleCalculator.Calculate(
            lineRequests,
            chargeRequests,
            sale.GeneralDiscountType,
            sale.GeneralDiscountValue).Total;
    }

    private static SaleBalanceStatus CalculateBalanceStatus(
        decimal total,
        decimal paidTotal)
    {
        if (paidTotal <= 0m)
        {
            return SaleBalanceStatus.Pending;
        }

        if (paidTotal >= total)
        {
            return SaleBalanceStatus.Paid;
        }

        return SaleBalanceStatus.PartiallyPaid;
    }

    private static DateTime? CalculateNextPaymentDueAtUtc(
        SaleResponse sale)
    {
        if (sale.Status != SaleStatus.Issued ||
            sale.OutstandingAmount <= 0m ||
            sale.IssuedAtUtc is null)
        {
            return null;
        }

        var latestValidPaymentDate =
            sale.Payments
                .Where(payment =>
                    payment.Status ==
                    SalePaymentStatus.Active)
                .Select(payment =>
                    (DateTime?)payment.PaidAtUtc)
                .Max();

        return (latestValidPaymentDate ??
                sale.IssuedAtUtc.Value)
            .AddDays(7);
    }

    private static string? NormalizeCabysCode(
        string? cabysCode)
    {
        if (string.IsNullOrWhiteSpace(cabysCode))
        {
            return null;
        }

        return cabysCode.Trim();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}