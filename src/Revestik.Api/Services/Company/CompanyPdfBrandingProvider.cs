using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Shared.Quotations;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Company;

// Separate orchestration: the PDF composers are intentionally I/O-free.
public sealed class CompanyPdfBrandingProvider(
    RevestikDbContext db,
    CompanyLogoStorage logoStorage,
    IOptions<CompanyOptions> legacyOptions)
{
    public Task<CompanyPdfBranding> ForPreviewAsync(CancellationToken ct) => PrepareAsync(false, null, ct);

    public async Task<CompanyPdfBranding?> ForQuotationAsync(int id, CancellationToken ct)
    {
        var document = await db.Quotations.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Status, x.CompanyIdentitySnapshotJson })
            .SingleOrDefaultAsync(ct);
        if (document is null) return null;
        return await PrepareAsync(document.Status == QuotationStatus.Issued,
            document.CompanyIdentitySnapshotJson, ct);
    }

    public async Task<CompanyPdfBranding?> ForSaleAsync(int id, CancellationToken ct)
    {
        var document = await db.Sales.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Status, x.CompanyIdentitySnapshotJson })
            .SingleOrDefaultAsync(ct);
        if (document is null) return null;
        return await PrepareAsync(document.Status != SaleStatus.Draft,
            document.CompanyIdentitySnapshotJson, ct);
    }

    private async Task<CompanyPdfBranding> PrepareAsync(bool issued, string? json, CancellationToken ct)
    {
        // A document with no snapshot is legacy only if it was issued.
        // Drafts always display the currently configured company.
        CompanyDocumentIdentity? historical;
        try
        {
            historical = issued
                ? CompanyDocumentIdentitySnapshotSerializer.Deserialize(json)
                : null;
        }
        catch (InvalidDataException ex)
        {
            throw new CompanyPdfSnapshotException(
                "Los datos históricos de identidad empresarial del documento son inválidos.", ex);
        }
        var profile = !issued
            ? await db.CompanyProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct)
            : null;
        var identity = CompanyDocumentIdentityResolver.Resolve(
            issued, historical, profile, legacyOptions.Value);

        if (identity.LogoStorageKey is not null)
        {
            if (identity.LogoContentType is not ("image/png" or "image/jpeg" or "image/webp"))
                throw new InvalidDataException("El tipo del logotipo empresarial histórico es inválido.");
            try
            {
                byte[] raster = await logoStorage.ReadAsync(identity.LogoStorageKey, ct);
                if (raster.Length == 0)
                    throw new InvalidDataException("El logotipo empresarial histórico está vacío.");
                return new CompanyPdfBranding(identity, null, raster);
            }
            catch (FileNotFoundException ex)
            {
                throw new InvalidDataException(
                    "No se encontró el archivo del logotipo empresarial histórico; el PDF no puede generarse.", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new InvalidDataException(
                    "No se encontró el directorio del logotipo empresarial histórico; el PDF no puede generarse.", ex);
            }
        }

        var svgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "revestik.svg");
        if (!File.Exists(svgPath))
            throw new FileNotFoundException("No se encontró el SVG predeterminado de Revestik.", svgPath);
        return new CompanyPdfBranding(identity, await File.ReadAllTextAsync(svgPath, ct), null);
    }
}
