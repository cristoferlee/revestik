using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public sealed class InventoryCostResolutionService(
    RevestikDbContext dbContext)
    : IInventoryCostResolutionService
{
    public async Task<IReadOnlyList<UnknownCostLayerResponse>>
        GetUnresolvedAsync(
            CancellationToken cancellationToken)
    {
        return await dbContext.InventoryCostLayers
            .AsNoTracking()
            .Where(layer =>
                !layer.Product.IsDeleted &&
                layer.RemainingQuantity > 0m &&
                !layer.UnitCost.HasValue &&
                !layer.ResolvedUnitCost.HasValue)
            .OrderBy(layer => layer.Product.Name)
            .ThenBy(layer => layer.CreatedAtUtc)
            .ThenBy(layer => layer.Id)
            .Select(layer =>
                new UnknownCostLayerResponse(
                    layer.Id,
                    layer.ProductId,
                    layer.Product.Name,
                    layer.Product.InventoryUnit.Symbol,
                    layer.OriginalQuantity,
                    layer.RemainingQuantity,
                    layer.SourceMovementId,
                    layer.SourceMovement.Type,
                    layer.SourceMovement.AdjustmentReason,
                    layer.SourceMovement.PhysicalCountId,
                    layer.SourceMovement.Notes,
                    layer.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResolvedInventoryCostResponse?> ResolveAsync(
        int layerId,
        ResolveInventoryCostRequest request,
        string resolvedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            resolvedByUserId,
            cancellationToken);

        if (request.UnitCost <= 0m)
        {
            throw new InvalidInventoryOperationException(
                "The resolved unit cost must be greater than zero.");
        }

        var layer = await dbContext.InventoryCostLayers
            .Include(item => item.Product)
            .SingleOrDefaultAsync(
                item => item.Id == layerId,
                cancellationToken);

        if (layer is null)
        {
            return null;
        }

        if (layer.Product.IsDeleted)
        {
            throw new InvalidInventoryOperationException(
                "The product is discontinued.");
        }

        if (layer.RemainingQuantity <= 0m)
        {
            throw new InvalidInventoryOperationException(
                "The cost layer no longer has remaining inventory.");
        }

        if (layer.UnitCost.HasValue ||
            layer.ResolvedUnitCost.HasValue)
        {
            throw new InvalidInventoryOperationException(
                "The cost layer already has a known cost.");
        }

        var now = DateTime.UtcNow;

        layer.ResolvedUnitCost = request.UnitCost;
        layer.CostResolvedByUserId = resolvedByUserId;
        layer.CostResolvedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResolvedInventoryCostResponse(
            layer.Id,
            layer.ProductId,
            layer.Product.Name,
            layer.RemainingQuantity,
            request.UnitCost,
            resolvedByUserId,
            now);
    }

    private async Task EnsureActiveUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidInventoryUserException();
        }

        var active = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Id == userId &&
                    user.IsActive,
                cancellationToken);

        if (!active)
        {
            throw new InvalidInventoryUserException();
        }
    }
}