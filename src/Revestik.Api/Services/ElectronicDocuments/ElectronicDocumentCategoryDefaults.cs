using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

internal static class ElectronicDocumentCategoryDefaults
{
    internal sealed record Definition(
        string SystemKey,
        string Name,
        AccountingNature AccountingNature,
        int SortOrder,
        OperationalDestination? DefaultOperationalDestination,
        bool AllowsAutomaticSuggestion = true);

    internal static readonly IReadOnlyList<Definition> All =
    [
        new(
            "inventory.merchandise",
            "Mercadería para reventa",
            AccountingNature.Inventory,
            100,
            null,
            false),

        new("expense.fuel", "Combustible", AccountingNature.OperatingExpense, 200, OperationalDestination.InternalExpense),
        new("expense.freight", "Transporte y fletes generales", AccountingNature.OperatingExpense, 210, OperationalDestination.InternalExpense),
        new("expense.utilities", "Servicios públicos", AccountingNature.OperatingExpense, 220, OperationalDestination.InternalExpense),
        new("expense.telecommunications", "Telecomunicaciones", AccountingNature.OperatingExpense, 230, OperationalDestination.InternalExpense),
        new("expense.maintenance", "Mantenimiento y reparaciones", AccountingNature.OperatingExpense, 240, OperationalDestination.InternalExpense),
        new("expense.marketing", "Publicidad y mercadeo", AccountingNature.OperatingExpense, 250, OperationalDestination.InternalExpense),
        new("expense.professional", "Honorarios / servicios profesionales", AccountingNature.OperatingExpense, 260, OperationalDestination.InternalExpense),
        new("expense.banking", "Servicios bancarios y comisiones", AccountingNature.OperatingExpense, 270, OperationalDestination.InternalExpense),
        new("expense.financial", "Gastos financieros", AccountingNature.OperatingExpense, 275, OperationalDestination.InternalExpense, false),
        new("expense.office-supplies", "Papelería y suministros", AccountingNature.OperatingExpense, 280, OperationalDestination.InternalExpense, false),
        new("expense.rent", "Arrendamientos", AccountingNature.OperatingExpense, 290, OperationalDestination.InternalExpense),
        new("expense.insurance", "Seguros", AccountingNature.OperatingExpense, 300, OperationalDestination.InternalExpense),
        new("expense.travel", "Viáticos", AccountingNature.OperatingExpense, 310, OperationalDestination.InternalExpense, false),
        new("expense.taxes-permits", "Impuestos, tasas y permisos", AccountingNature.OperatingExpense, 320, OperationalDestination.InternalExpense, false),
        new("expense.other-admin", "Otros gastos administrativos", AccountingNature.OperatingExpense, 390, OperationalDestination.InternalExpense, false),

        new("asset.tools-equipment", "Equipo y herramientas", AccountingNature.FixedAsset, 400, OperationalDestination.Asset, false),
        new("asset.computers", "Equipo de cómputo", AccountingNature.FixedAsset, 410, OperationalDestination.Asset, false),
        new("asset.furniture", "Mobiliario", AccountingNature.FixedAsset, 420, OperationalDestination.Asset, false),
        new("asset.vehicles", "Vehículos", AccountingNature.FixedAsset, 430, OperationalDestination.Asset, false),
        new("asset.other", "Otros activos", AccountingNature.FixedAsset, 490, OperationalDestination.Asset, false),

        // Keep the original key to upgrade the category created by the previous block
        // instead of creating a duplicate.
        new("direct.customer-project", "Mercadería directa para cliente", AccountingNature.DirectCost, 500, OperationalDestination.DirectCustomer, false),
        new("direct.installation", "Instalación / servicios subcontratados", AccountingNature.DirectCost, 510, OperationalDestination.DirectCustomer, false),
        new("direct.freight", "Transporte / entrega de proyecto", AccountingNature.DirectCost, 520, OperationalDestination.DirectCustomer, false),

        new("other.pending", "Otro / pendiente de clasificar", AccountingNature.Other, 900, null, false)
    ];

    internal static OperationalDestination? GetDefaultDestination(
        ElectronicDocumentCategory category) =>
        category.DefaultOperationalDestination;
}
