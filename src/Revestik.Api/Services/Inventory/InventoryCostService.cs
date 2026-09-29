using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Inventory;

public sealed class InventoryCostService(RevestikDbContext dbContext)
{
    public void CreateLayer(
        Product product,
        InventoryMovement sourceMovement,
        decimal quantity,
        decimal? unitCost,
        DateTime createdAtUtc)
    {
        if (quantity <= 0m)
            throw new InventoryCostIntegrityException(
                "A FIFO cost layer requires a positive quantity.");

        if (unitCost.HasValue && unitCost.Value <= 0m)
            throw new InventoryCostIntegrityException(
                "A FIFO cost layer unit cost must be positive when known.");

        dbContext.InventoryCostLayers.Add(new InventoryCostLayer
        {
            Product = product,
            SourceMovement = sourceMovement,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost,
            CreatedAtUtc = createdAtUtc
        });
    }

    public async Task ConsumeFifoAsync(
        Product product,
        InventoryMovement movement,
        decimal quantity,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0m)
            throw new InventoryCostIntegrityException(
                "FIFO consumption requires a positive quantity.");

        await EnsureBalanceAsync(
            product.Id,
            product.StockQuantity,
            cancellationToken);

        var layers = await dbContext.InventoryCostLayers
            .Where(x => x.ProductId == product.Id && x.RemainingQuantity > 0m)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var remaining = quantity;

        foreach (var layer in layers)
        {
            if (remaining == 0m)
                break;

            var consumed = Math.Min(layer.RemainingQuantity, remaining);
            layer.RemainingQuantity -= consumed;
            remaining -= consumed;

            dbContext.InventoryCostConsumptions.Add(
                new InventoryCostConsumption
                {
                    InventoryMovement = movement,
                    InventoryCostLayer = layer,
                    Quantity = consumed,
                    UnitCostSnapshot = layer.UnitCost,
                    CreatedAtUtc = createdAtUtc
                });
        }

        if (remaining > 0m)
            throw new InventoryCostIntegrityException(
                "FIFO cost layers do not contain enough quantity to match the current warehouse stock.");
    }

    public async Task EnsureBalanceAsync(
        int productId,
        decimal expectedStock,
        CancellationToken cancellationToken)
    {
        var remaining = await dbContext.InventoryCostLayers
            .Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)x.RemainingQuantity, cancellationToken)
            ?? 0m;

        if (remaining != expectedStock)
            throw new InventoryCostIntegrityException(
                $"FIFO cost layers are out of balance for product {productId}. Expected {expectedStock}, found {remaining}.");
    }
}