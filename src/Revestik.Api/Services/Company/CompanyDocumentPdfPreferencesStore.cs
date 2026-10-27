using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Company;

public static class CompanyDocumentPdfPreferencesStore
{
    public static async Task<CompanyDocumentPdfPreferences> ReadAsync(RevestikDbContext db, CancellationToken ct)
    {
        var value = await db.CompanyDocumentPdfSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        return value is null ? new CompanyDocumentPdfPreferences() : FromEntity(value);
    }

    public static CompanyDocumentPdfPreferences FromEntity(CompanyDocumentPdfSetting value) => new()
    {
        FooterMessage = value.FooterMessage,
        QuotationDefaultTerms = value.QuotationDefaultTerms,
        ShowWebsite = value.ShowWebsite,
        ShowQuotationSeller = value.ShowQuotationSeller
    };

    public static async Task<string> CaptureSnapshotAsync(RevestikDbContext db, CancellationToken ct) =>
        CompanyDocumentPdfPreferencesSnapshotSerializer.Serialize(await ReadAsync(db, ct));

    public static async Task<CompanyDocumentPdfPreferences> ForQuotationAsync(RevestikDbContext db, int id, CancellationToken ct)
    {
        var value = await db.Quotations.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Status, x.CompanyPdfPreferencesSnapshotJson }).SingleAsync(ct);
        return value.Status == Revestik.Shared.Quotations.QuotationStatus.Issued
            ? CompanyDocumentPdfPreferencesSnapshotSerializer.Deserialize(value.CompanyPdfPreferencesSnapshotJson) ?? new()
            : await ReadAsync(db, ct);
    }

    public static async Task<CompanyDocumentPdfPreferences> ForSaleAsync(RevestikDbContext db, int id, CancellationToken ct)
    {
        var value = await db.Sales.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Status, x.CompanyPdfPreferencesSnapshotJson }).SingleAsync(ct);
        return value.Status != Revestik.Shared.Sales.SaleStatus.Draft
            ? CompanyDocumentPdfPreferencesSnapshotSerializer.Deserialize(value.CompanyPdfPreferencesSnapshotJson) ?? new()
            : await ReadAsync(db, ct);
    }
}
