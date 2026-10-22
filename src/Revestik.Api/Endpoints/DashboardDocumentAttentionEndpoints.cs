using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Endpoints;

public static class DashboardDocumentAttentionEndpoints
{
    public static IEndpointRouteBuilder MapDashboardDocumentAttentionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard/document-attention-summary", async (
            RevestikDbContext db,
            CancellationToken cancellationToken) =>
        {
            var received = db.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Direction == ElectronicDocumentDirection.Received);

            var receivedCount = await received.CountAsync(cancellationToken);
            var pendingCount = await received.CountAsync(
                x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Pending,
                cancellationToken);

            // Reuse the two existing fiscal worklist definitions. Their queries are
            // disjoint: eligible unlinked invoices versus outstanding credit/debit notes.
            var worklists = new FiscalReconciliationCandidateService(db);
            var invoiceWork = await worklists.ListAsync("invoices", null, 1, 1, cancellationToken);
            var noteWork = await worklists.ListAsync("notes", null, 1, 1, cancellationToken);

            return Results.Ok(new DashboardDocumentAttentionSummaryResponse(
                receivedCount, pendingCount,
                invoiceWork.TotalCount + noteWork.TotalCount,
                invoiceWork.TotalCount, noteWork.TotalCount));
        })
        .WithName("GetDashboardDocumentAttentionSummary")
        .WithTags("Dashboard")
        .RequireAuthorization();

        endpoints.MapGet("/api/dashboard/documented-purchases-summary", async (
            RevestikDbContext db,
            CancellationToken cancellationToken) =>
        {
            var service = new ElectronicDocumentPeriodSummaryService(db);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"));
            var start = new DateOnly(localNow.Year, localNow.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var summary = await service.GetSummaryAsync(new ElectronicDocumentSummaryRequest
            {
                DateFrom = start,
                DateTo = end
            }, cancellationToken);
            return Results.Ok(new DashboardDocumentedPurchasesSummaryResponse(
                localNow.Year,
                localNow.Month,
                (summary.AccountingTotals ?? [])
                    .Select(x => new DashboardDocumentedPurchaseCurrencyResponse(
                        x.CurrencyCode, x.PurchasesTotal))
                    .ToArray()));
        })
        .WithName("GetDashboardDocumentedPurchasesSummary")
        .WithTags("Dashboard")
        .RequireAuthorization();

        return endpoints;
    }
}
