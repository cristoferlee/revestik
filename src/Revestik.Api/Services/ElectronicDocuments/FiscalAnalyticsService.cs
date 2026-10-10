using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>Fiscal evidence only. Totals must never be added to operational purchase totals.</summary>
public sealed class FiscalAnalyticsService(RevestikDbContext db)
{
    public async Task<FiscalAnalyticsResponse> GetAsync(DateOnly dateFrom, DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        if (dateTo < dateFrom || dateFrom == DateOnly.MinValue)
            throw new ArgumentException("El rango de fechas es inválido.");

        var from = new DateTimeOffset(dateFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-6));
        var until = new DateTimeOffset(dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-6));
        var data = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.FechaEmision >= from && x.FechaEmision < until)
            .Select(x => new { x.DocumentType, x.CurrencyCode, x.PurchaseId,
                x.ProcessingStatus, x.Direction, x.TotalDocument })
            .ToListAsync(cancellationToken);

        // Notes and receipts remain separately itemized evidence, never netted or
        // automatically included in purchase, expense, payment, or inventory ledgers.
        var rows = data
            .GroupBy(x => new { CurrencyCode = string.IsNullOrWhiteSpace(x.CurrencyCode)
                    ? "N/D" : x.CurrencyCode.ToUpper(), x.DocumentType })
            .OrderBy(x => x.Key.CurrencyCode).ThenBy(x => x.Key.DocumentType)
            .Select(g => new FiscalAnalyticsRow(g.Key.CurrencyCode, g.Key.DocumentType,
                g.Count(), g.Count(x => x.PurchaseId.HasValue),
                g.Count(x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Pending),
                g.Sum(x => x.TotalDocument)))
            .ToArray();
        return new FiscalAnalyticsResponse(dateFrom, dateTo, rows);
    }
}
