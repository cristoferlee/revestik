using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public sealed class InventoryService(RevestikDbContext dbContext)
    : IInventoryService
{
    private readonly InventoryCostService inventoryCostService = new(dbContext);

    public async Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        var activeProducts = dbContext.Products
            .AsNoTracking()
            .Where(product => !product.IsDeleted);

        var totalProducts = await activeProducts
            .CountAsync(cancellationToken);

        var inStockCount = await activeProducts
            .CountAsync(
                product =>
                    product.StockQuantity > product.MinimumStock,
                cancellationToken);

        var lowStockCount = await activeProducts
            .CountAsync(
                product =>
                    product.StockQuantity > 0m &&
                    product.StockQuantity <= product.MinimumStock,
                cancellationToken);

        var outOfStockCount = await activeProducts
            .CountAsync(
                product => product.StockQuantity == 0m,
                cancellationToken);

        var activeLayers = dbContext.InventoryCostLayers
            .AsNoTracking()
            .Where(layer =>
                !layer.Product.IsDeleted &&
                layer.RemainingQuantity > 0m);

        var totalInventoryCostValue = await activeLayers
            .Where(layer => layer.UnitCost.HasValue)
            .SumAsync(
                layer =>
                    layer.RemainingQuantity *
                    layer.UnitCost!.Value,
                cancellationToken);

        var unknownCostQuantity = await activeLayers
            .Where(layer => !layer.UnitCost.HasValue)
            .SumAsync(
                layer => layer.RemainingQuantity,
                cancellationToken);

        var productsWithUnknownCost = await activeLayers
            .Where(layer => !layer.UnitCost.HasValue)
            .Select(layer => layer.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new InventorySummaryResponse(
            totalProducts,
            inStockCount,
            lowStockCount,
            outOfStockCount,
            totalInventoryCostValue,
            unknownCostQuantity,
            productsWithUnknownCost);
    }

    public async Task<InventoryMovementResponse?> RegisterInitialStockAsync(
        int productId,
        InitialStockRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureActiveUserAsync(createdByUserId, cancellationToken);

        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && !p.IsDeleted,
            cancellationToken);

        if (product is null) return null;

        var exists = await dbContext.InventoryMovements.AsNoTracking().AnyAsync(
            m => m.ProductId == productId &&
                 m.Type == InventoryMovementType.InitialStock,
            cancellationToken);

        if (exists) throw new InitialStockAlreadyRegisteredException();

        ValidateQuantity(product, request.Quantity);

        if (product.StockQuantity != 0m)
            throw new InvalidInventoryOperationException(
                "Initial stock can only be registered while current stock is zero.");

        var now = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.InitialStock,
            QuantityChange = request.Quantity,
            StockBefore = 0m,
            StockAfter = request.Quantity,
            UnitCost = product.CurrentCost,
            AdjustmentReason = null,
            Notes = (request.Notes ?? string.Empty).Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now
        };

        product.StockQuantity = request.Quantity;
        product.UpdatedAtUtc = now;
        dbContext.InventoryMovements.Add(movement);

        if (request.Quantity > 0m)
            inventoryCostService.CreateLayer(
                product, movement, request.Quantity, product.CurrentCost, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryConcurrencyException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InitialStockAlreadyRegisteredException();
        }

        return MapMovement(movement);
    }

    public async Task<InventoryMovementResponse?> AdjustStockAsync(
        int productId,
        InventoryAdjustmentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureActiveUserAsync(createdByUserId, cancellationToken);

        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && !p.IsDeleted,
            cancellationToken);

        if (product is null) return null;

        await EnsureProductVersionCurrentAsync(
            product,
            cancellationToken);

        var hasInitialStock = await dbContext.InventoryMovements
            .AsNoTracking()
            .AnyAsync(
                m => m.ProductId == productId &&
                     m.Type == InventoryMovementType.InitialStock,
                cancellationToken);

        if (!hasInitialStock)
            throw new InvalidInventoryOperationException(
                "Initial stock must be registered before inventory adjustments.");

        ValidateQuantity(product, request.NewStockQuantity);

        var stockBefore = product.StockQuantity;
        var stockAfter = request.NewStockQuantity;
        var quantityChange = stockAfter - stockBefore;

        if (quantityChange == 0m)
            throw new InvalidInventoryOperationException(
                "The new stock must be different from the current stock.");

        if (!Enum.IsDefined(request.Reason))
            throw new InvalidInventoryOperationException(
                "The adjustment reason is invalid.");

        var notes = (request.Notes ?? string.Empty).Trim();
        if (notes.Length < 3)
            throw new InvalidInventoryOperationException(
                "The adjustment note must contain at least 3 characters.");

        await inventoryCostService.EnsureBalanceAsync(
            product.Id, stockBefore, cancellationToken);

        var now = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            Type = quantityChange > 0m
                ? InventoryMovementType.AdjustmentIncrease
                : InventoryMovementType.AdjustmentDecrease,
            QuantityChange = quantityChange,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            UnitCost = null,
            AdjustmentReason = request.Reason,
            Notes = notes,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now
        };

        dbContext.InventoryMovements.Add(movement);

        if (quantityChange > 0m)
        {
            inventoryCostService.CreateLayer(
                product, movement, quantityChange, null, now);
        }
        else
        {
            await inventoryCostService.ConsumeFifoAsync(
                product,
                movement,
                Math.Abs(quantityChange),
                now,
                cancellationToken);
        }

        product.StockQuantity = stockAfter;
        product.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryConcurrencyException();
        }

        return MapMovement(movement);
    }

    public async Task ConsumeSaleStockAsync(
        int productId,
        decimal commercialQuantity,
        int saleId,
        string saleNumber,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        if (commercialQuantity <= 0m)
            throw new InvalidInventoryOperationException(
                "Sale quantity must be greater than zero.");

        if (saleId <= 0)
            throw new InvalidInventoryOperationException(
                "Sale ID is required for inventory consumption.");

        if (string.IsNullOrWhiteSpace(saleNumber))
            throw new InvalidInventoryOperationException(
                "Sale number is required for inventory consumption.");

        await EnsureActiveUserAsync(
            createdByUserId,
            cancellationToken);

        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && !p.IsDeleted,
            cancellationToken);

        if (product is null)
            throw new InvalidInventoryOperationException(
                "The selected inventory product does not exist or is deleted.");

        await EnsureProductVersionCurrentAsync(
            product,
            cancellationToken);

        var hasInitialStock = await dbContext.InventoryMovements
            .AsNoTracking()
            .AnyAsync(
                m => m.ProductId == productId &&
                     m.Type == InventoryMovementType.InitialStock,
                cancellationToken);

        if (!hasInitialStock)
            throw new InvalidInventoryOperationException(
                "Initial stock must be registered before a product can be consumed by a sale.");

        if (product.CommercialUnitsPerInventoryUnit <= 0m)
            throw new InvalidInventoryOperationException(
                "The product inventory conversion is invalid.");

        var requestedPhysicalQuantity = decimal.Round(
            commercialQuantity /
            product.CommercialUnitsPerInventoryUnit,
            4,
            MidpointRounding.AwayFromZero);

        ValidateQuantity(
            product,
            requestedPhysicalQuantity);

        var stockBefore = product.StockQuantity;

        await inventoryCostService.EnsureBalanceAsync(
            product.Id,
            stockBefore,
            cancellationToken);

        if (stockBefore <= 0m)
        {
            return;
        }

        var consumedPhysicalQuantity =
            Math.Min(
                stockBefore,
                requestedPhysicalQuantity);

        if (consumedPhysicalQuantity <= 0m)
        {
            return;
        }

        var stockAfter =
            stockBefore - consumedPhysicalQuantity;

        var shortage =
            requestedPhysicalQuantity -
            consumedPhysicalQuantity;

        var now = DateTime.UtcNow;

        var notes = shortage > 0m
            ? $"Venta {saleNumber}. Salida de bodega: {consumedPhysicalQuantity:0.####}. Faltante directo/especial: {shortage:0.####}."
            : $"Venta {saleNumber}. Salida de bodega: {consumedPhysicalQuantity:0.####}.";

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            SaleId = saleId,
            Type = InventoryMovementType.Sale,
            QuantityChange = -consumedPhysicalQuantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            UnitCost = null,
            AdjustmentReason = null,
            Notes = notes,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now
        };

        dbContext.InventoryMovements.Add(movement);

        await inventoryCostService.ConsumeFifoAsync(
            product,
            movement,
            consumedPhysicalQuantity,
            now,
            cancellationToken);

        product.StockQuantity = stockAfter;
        product.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryConcurrencyException();
        }
    }

    public async Task ReverseSaleStockAsync(
        int saleId,
        string saleNumber,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        if (saleId <= 0)
            throw new InvalidInventoryOperationException(
                "Sale ID is required for inventory reversal.");

        if (string.IsNullOrWhiteSpace(saleNumber))
            throw new InvalidInventoryOperationException(
                "Sale number is required for inventory reversal.");

        await EnsureActiveUserAsync(
            createdByUserId,
            cancellationToken);

        var saleMovements = await dbContext.InventoryMovements
            .Where(movement =>
                movement.SaleId == saleId &&
                movement.Type == InventoryMovementType.Sale)
            .OrderBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        if (saleMovements.Count == 0)
            return;

        var movementIds = saleMovements
            .Select(movement => movement.Id)
            .ToArray();

        var reversedMovementIds = await dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement =>
                movement.ReversesInventoryMovementId.HasValue &&
                movementIds.Contains(
                    movement.ReversesInventoryMovementId.Value))
            .Select(movement =>
                movement.ReversesInventoryMovementId!.Value)
            .ToListAsync(cancellationToken);

        if (reversedMovementIds.Count == saleMovements.Count)
            return;

        if (reversedMovementIds.Count > 0)
        {
            throw new InventoryCostIntegrityException(
                "The sale inventory reversal is only partially recorded.");
        }

        var productIds = saleMovements
            .Select(movement => movement.ProductId)
            .Distinct()
            .ToArray();

        var products = await dbContext.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(
                product => product.Id,
                cancellationToken);

        if (products.Count != productIds.Length)
        {
            throw new InventoryCostIntegrityException(
                "One or more products referenced by the sale inventory history no longer exist.");
        }

        foreach (var product in products.Values)
        {
            await EnsureProductVersionCurrentAsync(
                product,
                cancellationToken,
                allowDeleted: true);

            await inventoryCostService.EnsureBalanceAsync(
                product.Id,
                product.StockQuantity,
                cancellationToken);
        }

        var consumptions =
            await inventoryCostService
                .GetValidatedRestorationConsumptionsAsync(
                    saleMovements,
                    cancellationToken);

        inventoryCostService.RestoreConsumptions(
            consumptions);

        var now = DateTime.UtcNow;

        foreach (var movement in saleMovements)
        {
            var product = products[movement.ProductId];
            var quantityToRestore =
                Math.Abs(movement.QuantityChange);

            var stockBefore = product.StockQuantity;
            var stockAfter =
                stockBefore + quantityToRestore;

            var reversal = new InventoryMovement
            {
                ProductId = product.Id,
                SaleId = saleId,
                ReversesInventoryMovementId = movement.Id,
                Type = InventoryMovementType.SaleReversal,
                QuantityChange = quantityToRestore,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                UnitCost = null,
                AdjustmentReason = null,
                Notes =
                    $"Anulación de venta {saleNumber}. Reposición de bodega: {quantityToRestore:0.####}.",
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = now
            };

            dbContext.InventoryMovements.Add(reversal);

            product.StockQuantity = stockAfter;
            product.UpdatedAtUtc = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryConcurrencyException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
                { Number: 2601 or 2627 })
        {
            throw new InventoryCostIntegrityException(
                "The sale inventory movement has already been reversed.");
        }
    }

    private async Task EnsureProductVersionCurrentAsync(
        Product product,
        CancellationToken cancellationToken,
        bool allowDeleted = false)
    {
        var databaseState = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == product.Id)
            .Select(p => new
            {
                p.RowVersion,
                p.IsDeleted
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (databaseState is null ||
            (!allowDeleted && databaseState.IsDeleted) ||
            !product.RowVersion.SequenceEqual(databaseState.RowVersion))
        {
            throw new InventoryConcurrencyException();
        }
    }

    private async Task EnsureActiveUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidInventoryUserException();

        var active = await dbContext.Users.AsNoTracking().AnyAsync(
            u => u.Id == userId && u.IsActive,
            cancellationToken);

        if (!active) throw new InvalidInventoryUserException();
    }

    private static void ValidateQuantity(Product product, decimal quantity)
    {
        if (quantity < 0m)
            throw new InvalidInventoryOperationException(
                "Inventory quantity cannot be negative.");

        if (product.RequiresWholeInventoryUnits &&
            quantity != decimal.Truncate(quantity))
            throw new InvalidInventoryOperationException(
                "This product requires whole inventory units.");
    }

    private static InventoryMovementResponse MapMovement(
        InventoryMovement movement) =>
        new(
            movement.Id,
            movement.ProductId,
            movement.Type,
            movement.QuantityChange,
            movement.StockBefore,
            movement.StockAfter,
            movement.UnitCost,
            movement.AdjustmentReason,
            movement.Notes,
            movement.CreatedByUserId,
            movement.CreatedAtUtc);
}