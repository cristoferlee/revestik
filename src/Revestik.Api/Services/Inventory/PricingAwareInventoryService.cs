using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.Inventory;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Inventory;

public sealed class PricingAwareInventoryService(
    InventoryService inner,
    RevestikDbContext dbContext)
    : IInventoryService
{
    public Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken) =>
        inner.GetSummaryAsync(cancellationToken);

    public Task<InventoryMovementResponse?> RegisterInitialStockAsync(
        int productId,
        InitialStockRequest request,
        string createdByUserId,
        CancellationToken cancellationToken) =>
        inner.RegisterInitialStockAsync(
            productId,
            request,
            createdByUserId,
            cancellationToken);

    public Task<InventoryMovementResponse?> AdjustStockAsync(
        int productId,
        InventoryAdjustmentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken) =>
        inner.AdjustStockAsync(
            productId,
            request,
            createdByUserId,
            cancellationToken);

    public async Task ConsumeSaleStockAsync(
        int productId,
        decimal billedQuantity,
        int saleId,
        string saleNumber,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        var pricing = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new
            {
                product.SalePriceBasis,
                product.CommercialUnitsPerInventoryUnit
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (pricing is null)
        {
            await inner.ConsumeSaleStockAsync(
                productId,
                billedQuantity,
                saleId,
                saleNumber,
                createdByUserId,
                cancellationToken);
            return;
        }

        var commercialQuantity =
            pricing.SalePriceBasis == SalePriceBasis.InventoryUnit
                ? decimal.Round(
                    billedQuantity *
                    pricing.CommercialUnitsPerInventoryUnit,
                    4,
                    MidpointRounding.AwayFromZero)
                : billedQuantity;

        await inner.ConsumeSaleStockAsync(
            productId,
            commercialQuantity,
            saleId,
            saleNumber,
            createdByUserId,
            cancellationToken);
    }

    public Task ReverseSaleStockAsync(
        int saleId,
        string saleNumber,
        string createdByUserId,
        CancellationToken cancellationToken) =>
        inner.ReverseSaleStockAsync(
            saleId,
            saleNumber,
            createdByUserId,
            cancellationToken);
}