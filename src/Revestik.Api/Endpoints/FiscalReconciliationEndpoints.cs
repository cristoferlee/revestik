using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Revestik.Api.Data;
using Revestik.Api.Authorization;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Endpoints;

public static class FiscalReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapFiscalReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchases/received-documents/fiscal")
            .WithTags("Fiscal reconciliation").RequireAuthorization(PolicyNames.ManagePurchases);

        group.MapGet("/analytics", async (DateOnly? dateFrom, DateOnly? dateTo,
            FiscalAnalyticsService service, CancellationToken ct) =>
        {
            var start = dateFrom ?? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = dateTo ?? start.AddMonths(1).AddDays(-1);
            if (end < start || start == DateOnly.MinValue || end == DateOnly.MaxValue)
                return Results.BadRequest(new { error = "Rango de fechas inválido." });
            return Results.Ok(await service.GetAsync(start, end, ct));
        });

        group.MapGet("/documents/{id:int}/purchases/{purchaseId:int}/review",
            async (int id, int purchaseId, ElectronicDocumentPurchaseReconciliationService service,
                CancellationToken ct) =>
            {
                try { return Results.Ok(await service.ReviewAsync(id, purchaseId, ct)); }
                catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
                catch (ArgumentOutOfRangeException e) { return Results.BadRequest(new { error = e.Message }); }
            });

        group.MapPost("/documents/{id:int}/link",
            async (int id, [FromBody] PurchaseLinkRequest request,
                ElectronicDocumentPurchaseReconciliationService review,
                ElectronicDocumentPurchaseLinkService links, CancellationToken ct) =>
            {
                try
                {
                    var assessment = await review.ReviewAsync(id, request.PurchaseId, ct);
                    if (!assessment.CanLinkUnderCurrentRules)
                        return Results.Conflict(new { error = "La vinculación fiscal presenta incompatibilidades.", assessment.Warnings });
                    await links.LinkAsync(id, request.PurchaseId, ct);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
                catch (ArgumentOutOfRangeException e) { return Results.BadRequest(new { error = e.Message }); }
                catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>();

        // Assisted discovery. All queries are read-only.
        group.MapGet("/worklist", async (string? kind, string? search, int? page, int? pageSize,
            RevestikDbContext db, CancellationToken ct) =>
        {
            try { return Results.Ok(await new FiscalReconciliationCandidateService(db)
                .ListAsync(kind ?? "invoices", search, page ?? 1, pageSize ?? 20, ct)); }
            catch (ArgumentOutOfRangeException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapGet("/documents/{id:int}/candidates", async (int id, int? days,
            RevestikDbContext db, CancellationToken ct) =>
        {
            try { return Results.Ok(await new FiscalReconciliationCandidateService(db).FindAsync(id, days ?? 30, ct)); }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (ArgumentOutOfRangeException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
        });

        group.MapGet("/documents/{id:int}/references", async (int id, RevestikDbContext db, CancellationToken ct) =>
        {
            try { return Results.Ok(await new FiscalReconciliationCandidateService(db).NoteAsync(id, ct)); }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
        });

        group.MapPost("/references/{referenceId:int}/link", async (int referenceId,
            [FromBody] FiscalReferenceLinkRequest request, RevestikDbContext db, CancellationToken ct) =>
        {
            try
            {
                await new ElectronicDocumentReferenceLinkService(db).LinkAsync(referenceId, request.DocumentId, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
        })
        .RequireAuthorization(PolicyNames.ManageAccountingClassification)
        .AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPost("/notes/{noteId:int}/approve", async (int noteId,
            [FromBody] FiscalApprovalRequest request, RevestikDbContext db, CancellationToken ct) =>
        {
            try
            {
                await new FiscalApprovalService(db).ApproveAsync(noteId, request, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
        })
        .RequireAuthorization(PolicyNames.ManageAccountingClassification)
        .AddEndpointFilter<AntiforgeryValidationFilter>();

        // Audited decision only: these endpoints never link a reference or approve a note.
        group.MapGet("/references/{referenceId:int}/resolution-history", async (
            int referenceId, RevestikDbContext db, CancellationToken ct) =>
        {
            try { return Results.Ok(await new FiscalReferenceResolutionService(db).GetHistoryAsync(referenceId, ct)); }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
        }).RequireAuthorization(PolicyNames.ManageAccountingClassification);

        group.MapPost("/references/{referenceId:int}/authorize-date-exception", async (
            int referenceId, [FromBody] FiscalDateExceptionRequest request,
            ClaimsPrincipal user, RevestikDbContext db, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            try
            {
                var decision = await new FiscalReferenceResolutionService(db)
                    .AuthorizeDateExceptionAsync(referenceId, request, userId, ct);
                return Results.Ok(decision);
            }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
        })
        .RequireAuthorization(PolicyNames.ManageAccountingClassification)
        .AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPost("/adjusted-totals", async ([FromBody] int[] documentIds,
            RevestikDbContext db, CancellationToken ct) =>
        {
            if (documentIds.Length > 100 || documentIds.Any(x => x <= 0))
                return Results.BadRequest(new { error = "Selección inválida (máximo 100 documentos)." });
            return Results.Ok(await new FiscalApprovalService(db).GetAdjustedTotalsAsync(documentIds, ct));
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        return endpoints;
    }
}