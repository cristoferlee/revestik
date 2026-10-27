using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;

namespace Revestik.Api.Services.Company;

// Fetches the single current company profile once per document issuance.
// No mutation or separate SaveChanges: the document snapshot is persisted with the issue.
public static class CompanyDocumentIdentityCaptureService
{
    public static async Task<string> CaptureSnapshotJsonAsync(
        RevestikDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var profile = await dbContext.CompanyProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Id == 1, cancellationToken);

        if (profile is null ||
            string.IsNullOrWhiteSpace(profile.LegalName) ||
            string.IsNullOrWhiteSpace(profile.IdentificationNumber))
        {
            throw new InvalidOperationException(
                "La identidad empresarial no está configurada correctamente. Revise Configuración > Mi empresa antes de emitir.");
        }

        return CompanyDocumentIdentitySnapshotSerializer.Serialize(
            CompanyDocumentIdentityResolver.Capture(profile));
    }
}
