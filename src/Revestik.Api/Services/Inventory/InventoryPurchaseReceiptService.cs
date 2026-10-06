using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public sealed class InventoryPurchaseReceiptService(RevestikDbContext dbContext) : IInventoryPurchaseReceiptService
{
    private readonly InventoryCostService inventoryCostService = new(dbContext);

    public async Task ReceiveAsync(int purchaseLineId, decimal inventoryUnitCost, string createdByUserId, CancellationToken cancellationToken)
    {
        if (purchaseLineId <= 0)
            throw new InvalidInventoryOperationException("Purchase line ID is required for inventory receipt.");
        if (inventoryUnitCost <= 0m)
            throw new InvalidInventoryOperationException("Purchase inventory unit cost must be greater than zero.");
        if (decimal.Round(inventoryUnitCost, 5, MidpointRounding.AwayFromZero) != inventoryUnitCost)
            throw new InvalidInventoryOperationException("Purchase inventory unit cost cannot have more than 5 decimals.");

        await EnsureActiveUserAsync(createdByUserId, cancellationToken);

        var alreadyApplied = await dbContext.InventoryMovements.AsNoTracking().AnyAsync(
            movement => movement.PurchaseLineId == purchaseLineId, cancellationToken);
        if (alreadyApplied) throw new PurchaseInventoryAlreadyAppliedException();

        var purchaseLine = await dbContext.PurchaseLines.AsNoTracking()
            .Where(line => line.Id == purchaseLineId)
            .Select(line => new { line.Id, line.PurchaseId, line.ProductId, line.Quantity })
            .SingleOrDefaultAsync(cancellationToken);
        if (purchaseLine is null)
            throw new InvalidInventoryOperationException("The purchase line does not exist.");

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == purchaseLine.ProductId && !product.IsDeleted && !product.IsArchived, cancellationToken);
        if (product is null)
            throw new InvalidInventoryOperationException("The selected inventory product does not exist or is inactive.");

        await EnsureProductVersionCurrentAsync(product, cancellationToken);
        ValidateQuantity(product, purchaseLine.Quantity);
        await inventoryCostService.EnsureBalanceAsync(product.Id, product.StockQuantity, cancellationToken);

        var now = DateTime.UtcNow;
        var stockBefore = product.StockQuantity;
        var stockAfter = stockBefore + purchaseLine.Quantity;
        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            PurchaseLineId = purchaseLine.Id,
            Type = InventoryMovementType.Purchase,
            QuantityChange = purchaseLine.Quantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            UnitCost = inventoryUnitCost,
            AdjustmentReason = null,
            Notes = $"Compra {purchaseLine.PurchaseId}. Recepción de bodega: {purchaseLine.Quantity:0.####}.",
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now
        };

        dbContext.InventoryMovements.Add(movement);
        inventoryCostService.CreateLayer(product, movement, purchaseLine.Quantity, inventoryUnitCost, now);
        product.StockQuantity = stockAfter;
        product.CurrentCost = inventoryUnitCost;
        product.UpdatedAtUtc = now;

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new InventoryConcurrencyException(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new PurchaseInventoryAlreadyAppliedException(); }
    }

    private async Task EnsureProductVersionCurrentAsync(Product product, CancellationToken cancellationToken)
    {
        var databaseState = await dbContext.Products.AsNoTracking()
            .Where(databaseProduct => databaseProduct.Id == product.Id)
            .Select(databaseProduct => new { databaseProduct.RowVersion, databaseProduct.IsDeleted, databaseProduct.IsArchived })
            .SingleOrDefaultAsync(cancellationToken);
        if (databaseState is null || databaseState.IsDeleted || databaseState.IsArchived ||
            !product.RowVersion.SequenceEqual(databaseState.RowVersion))
            throw new InventoryConcurrencyException();
    }

    private async Task EnsureActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new InvalidInventoryUserException();
        var active = await dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == userId && user.IsActive, cancellationToken);
        if (!active) throw new InvalidInventoryUserException();
    }

    private static void ValidateQuantity(Product product, decimal quantity)
    {
        if (quantity <= 0m)
            throw new InvalidInventoryOperationException("Purchase inventory quantity must be greater than zero.");
        if (product.RequiresWholeInventoryUnits && quantity != decimal.Truncate(quantity))
            throw new InvalidInventoryOperationException("This product requires whole inventory units.");
    }
}
