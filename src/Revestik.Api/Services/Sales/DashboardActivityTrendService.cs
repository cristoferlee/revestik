using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.Dashboard;
using Revestik.Shared.Sales;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.Sales;

public sealed class DashboardActivityTrendService(RevestikDbContext db)
{
    public async Task<DashboardActivityTrendResponse> GetAsync(int year, int month, CancellationToken ct)
    {
        if (year is < 2 or > 9998 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
        var first = new DateTime(year, month, 1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(first, zone);
        var throughUtc = TimeZoneInfo.ConvertTimeToUtc(first.AddMonths(1), zone);
        var last = DateTime.DaysInMonth(year, month);

        // Use the exact existing sales calculation. Never sum raw line prices.
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Issued && s.IssuedAtUtc >= fromUtc && s.IssuedAtUtc < throughUtc)
            .Select(s => new {
                s.Currency, s.IssuedAtUtc, s.GeneralDiscountType, s.GeneralDiscountValue,
                Lines = s.Lines.Select(l => new SaleLineRequest {
                    ProductId = l.ProductId, CabysCode = l.CabysCode ?? string.Empty,
                    Description = l.Description, Unit = l.Unit, Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice, DiscountType = l.DiscountType,
                    DiscountValue = l.DiscountValue, TaxRate = l.TaxRate
                }).ToList(),
                Charges = s.Charges.Select(c => new SaleChargeRequest {
                    Type = c.Type, Description = c.Description, Amount = c.Amount
                }).ToList()
            }).ToListAsync(ct);

        var saleRows = sales.Select(s => new {
            Currency = s.Currency.ToString(),
            Day = TimeZoneInfo.ConvertTimeFromUtc(s.IssuedAtUtc!.Value, zone).Day,
            Amount = SaleCalculator.Calculate(s.Lines, s.Charges, s.GeneralDiscountType, s.GeneralDiscountValue).Total
        }).ToList();

        var collections = await db.SalePayments.AsNoTracking()
            .Where(p => p.Status == SalePaymentStatus.Active && p.Sale.Status == SaleStatus.Issued
                && p.PaidAtUtc >= fromUtc && p.PaidAtUtc < throughUtc)
            .Select(p => new { p.Sale.Currency, p.PaidAtUtc, p.Amount })
            .ToListAsync(ct);
        var collectionRows = collections.Select(p => new {
            Currency = p.Currency.ToString(),
            Day = TimeZoneInfo.ConvertTimeFromUtc(p.PaidAtUtc, zone).Day,
            p.Amount
        }).ToList();

        var from = DateOnly.FromDateTime(first);
        var until = DateOnly.FromDateTime(first.AddMonths(1).AddDays(-1));
        // Purchases are an internal operational source, not received XML purchases.
        var purchases = await db.Purchases.AsNoTracking()
            .Where(p => p.PurchaseDate >= from && p.PurchaseDate <= until)
            .Select(p => new { p.Currency, p.PurchaseDate,
                Total = p.Lines.Sum(l => (decimal?)(l.Quantity * l.UnitCost)) ?? 0m })
            .ToListAsync(ct);

        var results = new List<DashboardActivityCurrencyResponse>();
        foreach (var currency in new[] { "CRC", "USD" })
        {
            var days = Enumerable.Range(1, last).Select(day => new DashboardActivityDayResponse(
                day,
                Round(saleRows.Where(x => x.Currency == currency && x.Day == day).Sum(x => x.Amount)),
                Round(collectionRows.Where(x => x.Currency == currency && x.Day == day).Sum(x => x.Amount)),
                Round(purchases.Where(x => x.Currency.ToString() == currency && x.PurchaseDate.Day == day).Sum(x => x.Total))
            )).ToArray();
            results.Add(new DashboardActivityCurrencyResponse(currency, days));
        }
        return new DashboardActivityTrendResponse(year, month, results);
    }

    private static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
