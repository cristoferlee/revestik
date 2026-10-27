using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Company;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.Quotations.Pdf;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Endpoints;

public static class CompanyDocumentPdfPreferencesEndpoints
{
    public sealed record UpdateRequest(string FooterMessage, string QuotationDefaultTerms,
        bool ShowWebsite, bool ShowQuotationSeller, string? Version);
    public sealed record Response(string FooterMessage, string QuotationDefaultTerms,
        bool ShowWebsite, bool ShowQuotationSeller, string? Version);

    public static IEndpointRouteBuilder MapCompanyDocumentPdfPreferencesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings/document-pdf")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Document PDF preferences");
        group.MapGet("/", async (RevestikDbContext db, CancellationToken ct) =>
        {
            var row = await db.CompanyDocumentPdfSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
            return Results.Ok(ToResponse(row));
        });
        // Render unsaved form values using the existing QuestPDF composer.
        // Preview is read-only: no quotation, sequence, snapshot, or settings update.
        group.MapPost("/preview", async (
            UpdateRequest request,
            CompanyPdfBrandingProvider brandingProvider,
            IQuotationPdfService pdfService,
            CancellationToken ct) =>
        {
            var preferences = new CompanyDocumentPdfPreferences
            {
                FooterMessage = request.FooterMessage,
                QuotationDefaultTerms = request.QuotationDefaultTerms,
                ShowWebsite = request.ShowWebsite,
                ShowQuotationSeller = request.ShowQuotationSeller
            };
            try
            {
                CompanyDocumentPdfPreferencesSnapshotSerializer.Validate(preferences);
            }
            catch (ArgumentException)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Preferences"] = ["Los textos son inválidos o superan el máximo permitido."]
                });
            }

            CompanyPdfBranding branding;
            try
            {
                branding = await brandingProvider.ForPreviewAsync(ct);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(title: "No se pudo preparar la vista previa.",
                    detail: "No está disponible el logotipo empresarial configurado.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var now = DateTime.UtcNow;
            var sample = new QuotationResponse
            {
                QuotationNumber = "VISTA PREVIA",
                CustomerName = "Cliente de ejemplo",
                CustomerIdentificationNumber = "Identificación de ejemplo",
                Currency = Currency.CRC,
                CreatedAtUtc = now,
                ValidUntilUtc = now.AddDays(15),
                CreatedByDisplayName = "Vendedor de ejemplo"
            };
            var pdf = pdfService.Generate(sample, branding, preferences);
            return Results.File(pdf, "application/pdf", "VistaPreviaCotizacion.pdf");
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPut("/", async (UpdateRequest request, RevestikDbContext db,
            ClaimsPrincipal principal, CancellationToken ct) =>
        {
            var values = new CompanyDocumentPdfPreferences {
                FooterMessage = request.FooterMessage,
                QuotationDefaultTerms = request.QuotationDefaultTerms,
                ShowWebsite = request.ShowWebsite,
                ShowQuotationSeller = request.ShowQuotationSeller
            };
            try { CompanyDocumentPdfPreferencesSnapshotSerializer.Validate(values); }
            catch (ArgumentException) { return Results.ValidationProblem(new Dictionary<string,string[]> { ["Preferences"] = ["Los textos son inválidos o superan el máximo permitido."] }); }

            // Serialize updates to this singleton (including first creation).
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var row = await db.CompanyDocumentPdfSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (row is null && request.Version is not null)
                return Results.Conflict(new { message = "La configuración cambió. Volvé a cargar." });
            if (row is not null)
            {
                if (string.IsNullOrEmpty(request.Version) ||
                    !TryParseVersion(request.Version, out var version) ||
                    !row.RowVersion.SequenceEqual(version))
                    return Results.Conflict(new { message = "La configuración cambió. Volvé a cargar." });
            }
            else
            {
                row = new CompanyDocumentPdfSetting();
                db.CompanyDocumentPdfSettings.Add(row);
            }
            var previous = CompanyDocumentPdfPreferencesStore.FromEntity(row);
            row.FooterMessage = values.FooterMessage;
            row.QuotationDefaultTerms = values.QuotationDefaultTerms;
            row.ShowWebsite = values.ShowWebsite;
            row.ShowQuotationSeller = values.ShowQuotationSeller;
            var changedFields = CompanyDocumentPdfChangeDetector.GetChangedFields(previous, values);
            if (changedFields.Count > 0)
                SecurityAudit.Record(db, "CompanyDocumentPdfPreferencesUpdated", "Company", "1",
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    new { preciseChanges = true, changed = changedFields });
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Results.Ok(ToResponse(row));
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { message = "La configuración fue modificada por otra sesión." });
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { message = "No se pudo guardar la configuración simultáneamente. Volvé a intentar." });
            }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }
    private static Response ToResponse(CompanyDocumentPdfSetting? row) => row is null
        ? new(string.Empty, string.Empty, true, true, null)
        : new(row.FooterMessage, row.QuotationDefaultTerms, row.ShowWebsite, row.ShowQuotationSeller,
            Convert.ToBase64String(row.RowVersion));

    private static bool TryParseVersion(string value, out byte[] version)
    {
        version = [];
        try { version = Convert.FromBase64String(value); return version.Length == 8; }
        catch (FormatException) { return false; }
    }
}
