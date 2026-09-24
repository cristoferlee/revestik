using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations;

public sealed class QuotationService(
    RevestikDbContext dbContext,
    IQuotationNumberGenerator quotationNumberGenerator)
    : IQuotationService
{
    public async Task<PaginatedResponse<QuotationListItemResponse>> GetPageAsync(
        QuotationListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.Quotations
            .AsNoTracking()
            .Include(quotation => quotation.Customer)
            .Include(quotation => quotation.Lines)
            .Include(quotation => quotation.Charges)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(quotation =>
                quotation.QuotationNumber.Contains(search) ||
                quotation.Customer.Name.Contains(search) ||
                quotation.Customer.IdentificationNumber.Contains(search) ||
                quotation.CustomerNameSnapshot.Contains(search) ||
                quotation.CustomerIdentificationNumberSnapshot.Contains(search));
        }

        if (request.DateFromUtc.HasValue)
        {
            query = query.Where(quotation =>
                (quotation.IssuedAtUtc ?? quotation.CreatedAtUtc) >=
                request.DateFromUtc.Value);
        }

        if (request.DateToUtc.HasValue)
        {
            query = query.Where(quotation =>
                (quotation.IssuedAtUtc ?? quotation.CreatedAtUtc) <=
                request.DateToUtc.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(quotation =>
                quotation.Status == request.Status.Value);
        }

        if (request.Currency.HasValue)
        {
            query = query.Where(quotation =>
                quotation.Currency == request.Currency.Value);
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var skip =
            ((long)request.Page - 1) *
            request.PageSize;

        IReadOnlyList<QuotationListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            var quotations = await query
                .OrderByDescending(quotation =>
                    quotation.IssuedAtUtc ??
                    quotation.CreatedAtUtc)
                .ThenByDescending(quotation => quotation.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var quotationIds = quotations
                .Select(quotation => quotation.Id)
                .ToList();

            var convertedSales = await dbContext.Sales
                .AsNoTracking()
                .Where(sale =>
                    sale.SourceQuotationId.HasValue &&
                    quotationIds.Contains(
                        sale.SourceQuotationId.Value))
                .OrderByDescending(sale => sale.Id)
                .Select(sale => new
                {
                    QuotationId =
                        sale.SourceQuotationId!.Value,
                    SaleId = sale.Id,
                    sale.SaleNumber
                })
                .ToListAsync(cancellationToken);

            var convertedSaleByQuotation =
                convertedSales
                    .GroupBy(sale => sale.QuotationId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First());

            items = quotations
                .Select(quotation =>
                {
                    convertedSaleByQuotation.TryGetValue(
                        quotation.Id,
                        out var convertedSale);

                    return new QuotationListItemResponse
                    {
                        Id = quotation.Id,
                        QuotationNumber =
                            quotation.QuotationNumber,
                        CreatedAtUtc =
                            quotation.CreatedAtUtc,
                        IssuedAtUtc =
                            quotation.IssuedAtUtc,
                        ValidUntilUtc =
                            quotation.ValidUntilUtc,
                        CustomerName =
                            GetCustomerName(quotation),
                        CustomerIdentificationNumber =
                            GetCustomerIdentificationNumber(
                                quotation),
                        Currency =
                            quotation.Currency,
                        Total =
                            CalculateQuotationTotal(
                                quotation),
                        Status =
                            quotation.Status,
                        ConvertedSaleId =
                            convertedSale?.SaleId,
                        ConvertedSaleNumber =
                            convertedSale?.SaleNumber ??
                            string.Empty
                    };
                })
                .ToList();
        }

        return new PaginatedResponse<QuotationListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var quotation = await dbContext.Quotations
            .AsNoTracking()
            .Where(quotation => quotation.Id == id)
            .Select(quotation => new QuotationResponse
            {
                Id = quotation.Id,
                QuotationNumber = quotation.QuotationNumber,
                CustomerId = quotation.CustomerId,
                CustomerName =
                    quotation.Status == QuotationStatus.Issued &&
                    quotation.CustomerNameSnapshot != string.Empty
                        ? quotation.CustomerNameSnapshot
                        : quotation.Customer.Name,
                CustomerIdentificationNumber =
                    quotation.Status == QuotationStatus.Issued &&
                    quotation.CustomerIdentificationNumberSnapshot != string.Empty
                        ? quotation.CustomerIdentificationNumberSnapshot
                        : quotation.Customer.IdentificationNumber,
                CustomerEmail =
                    quotation.Status == QuotationStatus.Issued &&
                    quotation.CustomerEmailSnapshot != string.Empty
                        ? quotation.CustomerEmailSnapshot
                        : quotation.Customer.Email,
                CustomerPhoneNumber =
                    quotation.Status == QuotationStatus.Issued &&
                    quotation.CustomerPhoneNumberSnapshot != string.Empty
                        ? quotation.CustomerPhoneNumberSnapshot
                        : quotation.Customer.PhoneNumber,
                Currency = quotation.Currency,
                Status = quotation.Status,
                IssuedAtUtc = quotation.IssuedAtUtc,
                ValidUntilUtc = quotation.ValidUntilUtc,
                Observations = quotation.Observations,
                CreatedByUserId = quotation.CreatedByUserId,
                CreatedByDisplayName =
                    quotation.CreatedByUser.DisplayName,
                CreatedAtUtc = quotation.CreatedAtUtc,
                UpdatedAtUtc = quotation.UpdatedAtUtc,

                Lines = quotation.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new QuotationLineResponse
                    {
                        Id = line.Id,
                        ProductId = line.ProductId,
                        CabysCode =
                            line.CabysCode ??
                            string.Empty,
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
                    .ToList(),

                Charges = quotation.Charges
                    .OrderBy(charge => charge.Id)
                    .Select(charge =>
                        new QuotationChargeResponse
                        {
                            Id = charge.Id,
                            Type = charge.Type,
                            Description =
                                charge.Description,
                            Amount = charge.Amount
                        })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (quotation is null)
        {
            return null;
        }

        PopulateCalculatedTotals(quotation);

        return quotation;
    }

    public async Task<QuotationResponse> CreateAsync(
        QuotationUpsertRequest request,
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

        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer =>
                    customer.Id == request.CustomerId &&
                    customer.IsActive,
                cancellationToken);

        if (!customerExists)
        {
            throw new InvalidOperationException(
                "The customer does not exist or is inactive.");
        }

        var creatorExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Id == createdByUserId &&
                    user.IsActive,
                cancellationToken);

        if (!creatorExists)
        {
            throw new InvalidOperationException(
                "The creator user does not exist or is inactive.");
        }

        var quotationNumber =
            await quotationNumberGenerator.GenerateAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        var quotation = new Quotation
        {
            QuotationNumber =
                quotationNumber,
            CustomerId =
                request.CustomerId,
            CreatedByUserId =
                createdByUserId,
            Currency =
                request.Currency,
            Status =
                QuotationStatus.Draft,
            IssuedAtUtc =
                null,
            ValidUntilUtc =
                request.ValidUntilUtc,
            Observations =
                request.Observations.Trim(),
            CreatedAtUtc =
                now,

            Lines = request.Lines
                .Select(line => new QuotationLine
                {
                    ProductId =
                        line.ProductId,
                    CabysCode =
                        NormalizeCabysCode(
                            line.CabysCode),
                    Description =
                        line.Description.Trim(),
                    Unit =
                        line.Unit.Trim(),
                    Quantity =
                        line.Quantity,
                    UnitPrice =
                        line.UnitPrice,
                    DiscountType =
                        line.DiscountType,
                    DiscountValue =
                        line.DiscountValue,
                    TaxRate =
                        line.TaxRate
                })
                .ToList(),

            Charges = request.Charges
                .Select(charge =>
                    new QuotationCharge
                    {
                        Type =
                            charge.Type,
                        Description =
                            charge.Description.Trim(),
                        Amount =
                            charge.Amount
                    })
                .ToList()
        };

        dbContext.Quotations.Add(quotation);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            quotation.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The quotation could not be loaded after creation.");
    }

    public async Task<QuotationResponse?> UpdateAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quotation = await dbContext.Quotations
            .Include(quotation => quotation.Lines)
            .Include(quotation => quotation.Charges)
            .SingleOrDefaultAsync(
                quotation => quotation.Id == id,
                cancellationToken);

        if (quotation is null)
        {
            return null;
        }

        await ApplyRequestAsync(
            quotation,
            request,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            quotation.Id,
            cancellationToken);
    }

    public async Task<QuotationResponse?> IssueAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quotation = await dbContext.Quotations
            .Include(quotation => quotation.Lines)
            .Include(quotation => quotation.Charges)
            .SingleOrDefaultAsync(
                quotation => quotation.Id == id,
                cancellationToken);

        if (quotation is null)
        {
            return null;
        }

        var customer = await ApplyRequestAsync(
            quotation,
            request,
            cancellationToken);

        quotation.CustomerNameSnapshot =
            customer.Name;
        quotation.CustomerIdentificationNumberSnapshot =
            customer.IdentificationNumber;
        quotation.CustomerEmailSnapshot =
            customer.Email;
        quotation.CustomerPhoneNumberSnapshot =
            customer.PhoneNumber;

        quotation.Status =
            QuotationStatus.Issued;
        quotation.IssuedAtUtc =
            DateTime.UtcNow;
        quotation.UpdatedAtUtc =
            quotation.IssuedAtUtc;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetByIdAsync(
            quotation.Id,
            cancellationToken);
    }

    private async Task<Customer> ApplyRequestAsync(
        Quotation quotation,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                customer =>
                    customer.Id ==
                    request.CustomerId &&
                    customer.IsActive,
                cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException(
                "The customer does not exist or is inactive.");
        }

        quotation.CustomerId =
            request.CustomerId;
        quotation.Currency =
            request.Currency;
        quotation.ValidUntilUtc =
            request.ValidUntilUtc;
        quotation.Observations =
            request.Observations.Trim();
        quotation.UpdatedAtUtc =
            DateTime.UtcNow;

        dbContext.QuotationLines.RemoveRange(
            quotation.Lines);

        dbContext.QuotationCharges.RemoveRange(
            quotation.Charges);

        quotation.Lines = request.Lines
            .Select(line => new QuotationLine
            {
                ProductId =
                    line.ProductId,
                CabysCode =
                    NormalizeCabysCode(
                        line.CabysCode),
                Description =
                    line.Description.Trim(),
                Unit =
                    line.Unit.Trim(),
                Quantity =
                    line.Quantity,
                UnitPrice =
                    line.UnitPrice,
                DiscountType =
                    line.DiscountType,
                DiscountValue =
                    line.DiscountValue,
                TaxRate =
                    line.TaxRate
            })
            .ToList();

        quotation.Charges =
            request.Charges
                .Select(charge =>
                    new QuotationCharge
                    {
                        Type = charge.Type,
                        Description =
                            charge.Description.Trim(),
                        Amount =
                            charge.Amount
                    })
                .ToList();

        return customer;
    }

    private static string GetCustomerName(
        Quotation quotation) =>
        quotation.Status == QuotationStatus.Issued &&
        !string.IsNullOrWhiteSpace(
            quotation.CustomerNameSnapshot)
            ? quotation.CustomerNameSnapshot
            : quotation.Customer.Name;

    private static string GetCustomerIdentificationNumber(
        Quotation quotation) =>
        quotation.Status == QuotationStatus.Issued &&
        !string.IsNullOrWhiteSpace(
            quotation.CustomerIdentificationNumberSnapshot)
            ? quotation.CustomerIdentificationNumberSnapshot
            : quotation.Customer.IdentificationNumber;

    private static decimal CalculateQuotationTotal(
        Quotation quotation)
    {
        var linesTotal =
            quotation.Lines.Sum(line =>
            {
                var calculation =
                    QuotationCalculator.CalculateLine(
                        new QuotationLineRequest
                        {
                            ProductId =
                                line.ProductId,
                            CabysCode =
                                line.CabysCode ??
                                string.Empty,
                            Description =
                                line.Description,
                            Unit =
                                line.Unit,
                            Quantity =
                                line.Quantity,
                            UnitPrice =
                                line.UnitPrice,
                            DiscountType =
                                line.DiscountType,
                            DiscountValue =
                                line.DiscountValue,
                            TaxRate =
                                line.TaxRate
                        });

                return calculation.TotalAmount;
            });

        return Round(
            linesTotal +
            quotation.Charges.Sum(
                charge => charge.Amount));
    }

    private static void PopulateCalculatedTotals(
        QuotationResponse quotation)
    {
        decimal subtotal = 0m;
        decimal discountTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var line in quotation.Lines)
        {
            var request =
                new QuotationLineRequest
                {
                    ProductId =
                        line.ProductId,
                    CabysCode =
                        line.CabysCode,
                    Description =
                        line.Description,
                    Unit =
                        line.Unit,
                    Quantity =
                        line.Quantity,
                    UnitPrice =
                        line.UnitPrice,
                    DiscountType =
                        line.DiscountType,
                    DiscountValue =
                        line.DiscountValue,
                    TaxRate =
                        line.TaxRate
                };

            var calculation =
                QuotationCalculator.CalculateLine(
                    request);

            line.BaseAmount =
                calculation.BaseAmount;
            line.DiscountAmount =
                calculation.DiscountAmount;
            line.TaxAmount =
                calculation.TaxAmount;
            line.TotalAmount =
                calculation.TotalAmount;

            subtotal +=
                calculation.BaseAmount;
            discountTotal +=
                calculation.DiscountAmount;
            taxTotal +=
                calculation.TaxAmount;
        }

        var chargeTotal =
            quotation.Charges.Sum(
                charge => charge.Amount);

        quotation.Subtotal =
            Round(subtotal);
        quotation.DiscountTotal =
            Round(discountTotal);
        quotation.TaxTotal =
            Round(taxTotal);
        quotation.ChargeTotal =
            Round(chargeTotal);

        quotation.Total =
            Round(
                quotation.Lines.Sum(
                    line => line.TotalAmount) +
                chargeTotal);
    }

    private static string? NormalizeCabysCode(
        string? cabysCode)
    {
        if (string.IsNullOrWhiteSpace(
                cabysCode))
        {
            return null;
        }

        return cabysCode.Trim();
    }

    private static decimal Round(
        decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}