using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public sealed class InventoryPhysicalCountService(
    RevestikDbContext dbContext,
    IInventoryService inventoryService)
    : IInventoryPhysicalCountService
{
    public async Task<PhysicalCountResponse> StartAsync(
        PhysicalCountCreateRequest request,
        string startedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            startedByUserId,
            cancellationToken);

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => !product.IsDeleted)
            .OrderBy(product => product.Id)
            .Select(product => new
            {
                product.Id,
                product.StockQuantity
            })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            throw new InvalidInventoryOperationException(
                "A physical count cannot be started without active products.");
        }

        var now = DateTime.UtcNow;

        var physicalCount = new InventoryPhysicalCount
        {
            Status = PhysicalCountStatus.Draft,
            Notes = (request.Notes ?? string.Empty).Trim(),
            StartedByUserId = startedByUserId,
            StartedAtUtc = now,
            Lines = products
                .Select(product =>
                    new InventoryPhysicalCountLine
                    {
                        ProductId = product.Id,
                        ExpectedQuantity =
                            product.StockQuantity
                    })
                .ToList()
        };

        dbContext.InventoryPhysicalCounts.Add(
            physicalCount);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        var created = await LoadAsync(
            physicalCount.Id,
            asTracking: false,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The physical count could not be loaded after creation.");

        return Map(created);
    }

    public async Task<PhysicalCountResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var physicalCount =
            await LoadAsync(
                id,
                asTracking: false,
                cancellationToken);

        return physicalCount is null
            ? null
            : Map(physicalCount);
    }

    public async Task<PhysicalCountResponse?> UpdateLinesAsync(
        int id,
        PhysicalCountUpdateRequest request,
        string updatedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveUserAsync(
            updatedByUserId,
            cancellationToken);

        if (request.Lines.Count == 0)
        {
            throw new InvalidInventoryOperationException(
                "At least one physical count line is required.");
        }

        var duplicateLineId = request.Lines
            .GroupBy(line => line.LineId)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateLineId.HasValue)
        {
            throw new InvalidInventoryOperationException(
                $"Physical count line {duplicateLineId.Value} was submitted more than once.");
        }

        var physicalCount =
            await LoadAsync(
                id,
                asTracking: true,
                cancellationToken);

        if (physicalCount is null)
            return null;

        EnsureDraft(physicalCount);

        var linesById = physicalCount.Lines
            .ToDictionary(line => line.Id);

        foreach (var update in request.Lines)
        {
            if (!linesById.TryGetValue(
                    update.LineId,
                    out var line))
            {
                throw new InvalidInventoryOperationException(
                    $"Physical count line {update.LineId} does not belong to count {id}.");
            }

            ValidateCountedQuantity(
                line.Product,
                update.CountedQuantity);

            line.CountedQuantity =
                update.CountedQuantity;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(physicalCount);
    }

    public async Task<PhysicalCountResponse?> CompleteAsync(
        int id,
        string completedByUserId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(
            completedByUserId,
            cancellationToken);

        await using var transaction =
            dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(
                    cancellationToken)
                : null;

        try
        {
            var physicalCount =
                await LoadAsync(
                    id,
                    asTracking: true,
                    cancellationToken);

            if (physicalCount is null)
                return null;

            EnsureDraft(physicalCount);

            if (physicalCount.Lines.Any(
                    line => !line.CountedQuantity.HasValue))
            {
                throw new InvalidInventoryOperationException(
                    "Every physical count line must have a counted quantity before completion.");
            }

            ValidateCompletionSnapshot(
                physicalCount);

            foreach (var line in physicalCount.Lines)
            {
                var countedQuantity =
                    line.CountedQuantity!.Value;

                if (countedQuantity ==
                    line.ExpectedQuantity)
                {
                    continue;
                }

                var movement =
                    await inventoryService.AdjustStockAsync(
                        line.ProductId,
                        new InventoryAdjustmentRequest
                        {
                            NewStockQuantity =
                                countedQuantity,
                            Reason =
                                InventoryAdjustmentReason.PhysicalCount,
                            Notes =
                                $"Conteo físico #{physicalCount.Id}."
                        },
                        completedByUserId,
                        cancellationToken);

                if (movement is null)
                {
                    throw new InventoryPhysicalCountConflictException(
                        $"Product {line.ProductId} is no longer available for physical count completion.");
                }

                var trackedMovement =
                    dbContext.InventoryMovements.Local
                        .SingleOrDefault(item =>
                            item.Id == movement.Id)
                    ?? await dbContext.InventoryMovements
                        .SingleAsync(
                            item =>
                                item.Id == movement.Id,
                            cancellationToken);

                trackedMovement.PhysicalCountId =
                    physicalCount.Id;
            }

            physicalCount.Status =
                PhysicalCountStatus.Completed;
            physicalCount.CompletedByUserId =
                completedByUserId;
            physicalCount.CompletedAtUtc =
                DateTime.UtcNow;

            await dbContext.SaveChangesAsync(
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return Map(physicalCount);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(
                    cancellationToken);
            }

            throw;
        }
    }

    private async Task<InventoryPhysicalCount?> LoadAsync(
        int id,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<InventoryPhysicalCount> query =
            dbContext.InventoryPhysicalCounts
                .Include(count => count.Lines)
                    .ThenInclude(line => line.Product)
                        .ThenInclude(product =>
                            product.InventoryUnit);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .SingleOrDefaultAsync(
                count => count.Id == id,
                cancellationToken);
    }

    private static void EnsureDraft(
        InventoryPhysicalCount physicalCount)
    {
        if (physicalCount.Status !=
            PhysicalCountStatus.Draft)
        {
            throw new InvalidInventoryOperationException(
                "Only draft physical counts can be modified or completed.");
        }
    }

    private static void ValidateCompletionSnapshot(
        InventoryPhysicalCount physicalCount)
    {
        foreach (var line in physicalCount.Lines)
        {
            if (line.Product.IsDeleted)
            {
                throw new InventoryPhysicalCountConflictException(
                    $"Product {line.ProductId} was discontinued after the physical count started.");
            }

            if (line.Product.StockQuantity !=
                line.ExpectedQuantity)
            {
                throw new InventoryPhysicalCountConflictException(
                    $"Product {line.ProductId} stock changed after the physical count started. " +
                    "The count must be reviewed before completion.");
            }

            ValidateCountedQuantity(
                line.Product,
                line.CountedQuantity!.Value);
        }
    }

    private static void ValidateCountedQuantity(
        Product product,
        decimal quantity)
    {
        if (quantity < 0m)
        {
            throw new InvalidInventoryOperationException(
                "Counted inventory quantity cannot be negative.");
        }

        if (decimal.Round(quantity, 4) != quantity)
        {
            throw new InvalidInventoryOperationException(
                "Counted inventory quantity cannot have more than four decimal places.");
        }

        if (product.RequiresWholeInventoryUnits &&
            quantity != decimal.Truncate(quantity))
        {
            throw new InvalidInventoryOperationException(
                "This product requires whole inventory units.");
        }
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

    private static PhysicalCountResponse Map(
        InventoryPhysicalCount physicalCount)
    {
        var lines = physicalCount.Lines
            .OrderBy(line => line.Product.Name)
            .ThenBy(line => line.ProductId)
            .Select(line =>
                new PhysicalCountLineResponse(
                    line.Id,
                    line.ProductId,
                    line.Product.Name,
                    line.Product.InventoryUnit.Symbol,
                    line.Product.RequiresWholeInventoryUnits,
                    line.ExpectedQuantity,
                    line.CountedQuantity,
                    line.CountedQuantity.HasValue
                        ? line.CountedQuantity.Value -
                          line.ExpectedQuantity
                        : null))
            .ToList();

        return new PhysicalCountResponse(
            physicalCount.Id,
            physicalCount.Status,
            physicalCount.Notes,
            physicalCount.StartedByUserId,
            physicalCount.StartedAtUtc,
            physicalCount.CompletedByUserId,
            physicalCount.CompletedAtUtc,
            lines);
    }
}
