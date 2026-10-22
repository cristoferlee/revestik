using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Authorization;
using Revestik.Shared.Dashboard;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Dashboard;

/// <summary>Read-only cross-module snapshot; does not create an audit trail.</summary>
public sealed class DashboardRecentActivityService(RevestikDbContext db)
{
    public async Task<DashboardRecentActivityResponse> GetAsync(
        ClaimsPrincipal user, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(limit));
        var admin = user.IsInRole(RoleNames.Administrator);
        var commercial = admin || user.IsInRole(RoleNames.Accountant) || user.IsInRole(RoleNames.Sales);
        var warehouse = user.IsInRole(RoleNames.Warehouse);
        var items = new List<DashboardRecentActivityItemResponse>();

        if (commercial)
        {
            var sales = await db.Sales.AsNoTracking()
                .Where(x => x.Status == SaleStatus.Issued && x.IssuedAtUtc != null)
                .OrderByDescending(x => x.IssuedAtUtc).Take(limit)
                .Select(x => new { x.Id, x.SaleNumber, x.IssuedAtUtc }).ToListAsync(ct);
            items.AddRange(sales.Select(x => new DashboardRecentActivityItemResponse(
                "SaleIssued", "Venta emitida", x.SaleNumber ?? $"Venta #{x.Id}",
                x.IssuedAtUtc!.Value, "/sales/history")));

            var payments = await db.SalePayments.AsNoTracking()
                .Where(x => x.Status == SalePaymentStatus.Active && x.Sale.Status == SaleStatus.Issued)
                .OrderByDescending(x => x.CreatedAtUtc).Take(limit)
                .Select(x => new { x.Id, x.SaleId, x.CreatedAtUtc }).ToListAsync(ct);
            items.AddRange(payments.Select(x => new DashboardRecentActivityItemResponse(
                "SalePayment", "Cobro registrado", $"Venta #{x.SaleId}",
                x.CreatedAtUtc, "/sales/history")));

            var customers = await db.Customers.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
                .Take(limit).Select(x => new { x.Id, x.Name, x.CreatedAtUtc }).ToListAsync(ct);
            items.AddRange(customers.Select(x => new DashboardRecentActivityItemResponse(
                "CustomerCreated", "Cliente registrado", x.Name, x.CreatedAtUtc, "/customers")));

            var projects = await db.Set<Project>().AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
                .Take(limit).Select(x => new { x.Id, x.Name, x.CreatedAtUtc }).ToListAsync(ct);
            items.AddRange(projects.Select(x => new DashboardRecentActivityItemResponse(
                "ProjectCreated", "Proyecto creado", x.Name, x.CreatedAtUtc, "/projects")));
        }

        if (commercial || warehouse)
        {
            var purchases = await db.Purchases.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
                .Take(limit).Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct);
            items.AddRange(purchases.Select(x => new DashboardRecentActivityItemResponse(
                "PurchaseCreated", "Compra registrada", $"Compra #{x.Id}", x.CreatedAtUtc, "/purchases/history")));

            var documents = await db.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Direction == Revestik.Shared.ElectronicDocuments.ElectronicDocumentDirection.Received)
                .OrderByDescending(x => x.ImportedAtUtc).Take(limit)
                .Select(x => new { x.NumeroConsecutivo, x.IssuerName, x.ImportedAtUtc }).ToListAsync(ct);
            items.AddRange(documents.Select(x => new DashboardRecentActivityItemResponse(
                "DocumentImported", "Documento recibido",
                string.IsNullOrWhiteSpace(x.IssuerName)
                    ? (string.IsNullOrWhiteSpace(x.NumeroConsecutivo) ? "Documento sin número" : x.NumeroConsecutivo)
                    : (string.IsNullOrWhiteSpace(x.NumeroConsecutivo)
                        ? x.IssuerName
                        : $"{x.IssuerName} · {x.NumeroConsecutivo}"),
                x.ImportedAtUtc, "/purchases/received-documents")));
        }

        if (commercial || warehouse)
        {
            var products = await db.Products.AsNoTracking().Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAtUtc).Take(limit)
                .Select(x => new { x.Name, x.CreatedAtUtc }).ToListAsync(ct);
            items.AddRange(products.Select(x => new DashboardRecentActivityItemResponse(
                "ProductCreated", "Producto registrado", x.Name, x.CreatedAtUtc, "/inventory")));
        }

        // Lifecycle history endpoint requires AdministratorOnly; preserve that confidentiality here.
        if (admin)
        {
            var transitions = await db.Set<ProjectLifecycleChange>().AsNoTracking()
                .OrderByDescending(x => x.ChangedAtUtc).Take(limit)
                .Select(x => new { x.ProjectId, x.NewStatus, x.ChangedAtUtc }).ToListAsync(ct);
            items.AddRange(transitions.Select(x => new DashboardRecentActivityItemResponse(
                "ProjectStatusChanged", "Estado de proyecto actualizado",
                $"Proyecto #{x.ProjectId} · {x.NewStatus}", x.ChangedAtUtc, "/projects")));
        }

        return new DashboardRecentActivityResponse(items
            .Where(x => x.OccurredAtUtc != default)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenBy(x => x.Kind, StringComparer.Ordinal)
            .Take(limit).ToList());
    }
}
