using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryPhysicalCountServiceTests
{
    private const string UserId =
        "physical-count-service-user";

    [Fact]
    public async Task Start_SnapshotsActiveProductsWithoutChangingStock()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        var active = await SeedProductAsync(
            db,
            "Activo",
            12m);

        var deleted = await SeedProductAsync(
            db,
            "Descontinuado",
            7m);

        deleted.IsDeleted = true;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.StartAsync(
            new PhysicalCountCreateRequest
            {
                Notes = "Conteo mensual."
            },
            UserId,
            CancellationToken.None);

        Assert.Equal(PhysicalCountStatus.Draft, result.Status);
        Assert.Equal("Conteo mensual.", result.Notes);

        var line = Assert.Single(result.Lines);
        Assert.Equal(active.Id, line.ProductId);
        Assert.Equal(12m, line.ExpectedQuantity);
        Assert.Null(line.CountedQuantity);

        Assert.Equal(12m, active.StockQuantity);
        Assert.Equal(7m, deleted.StockQuantity);
    }

    [Fact]
    public async Task Start_WithoutActiveProducts_Throws()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.StartAsync(
                new PhysicalCountCreateRequest(),
                UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateLines_SavesCountsWithoutChangingInventory()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        var product = await SeedProductAsync(
            db,
            "Producto",
            10m);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        var line = Assert.Single(started.Lines);

        var updated = await service.UpdateLinesAsync(
            started.Id,
            new PhysicalCountUpdateRequest
            {
                Lines =
                [
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = line.Id,
                        CountedQuantity = 8m
                    }
                ]
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(8m, updated.Lines.Single().CountedQuantity);
        Assert.Equal(-2m, updated.Lines.Single().DifferenceQuantity);
        Assert.Equal(10m, product.StockQuantity);

        Assert.False(
            await db.InventoryMovements.AnyAsync(
                movement =>
                    movement.AdjustmentReason ==
                    InventoryAdjustmentReason.PhysicalCount));
    }

    [Fact]
    public async Task UpdateLines_FractionalWholeUnitQuantity_Throws()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        await SeedProductAsync(
            db,
            "Caja completa",
            10m,
            requiresWholeUnits: true);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.UpdateLinesAsync(
                started.Id,
                new PhysicalCountUpdateRequest
                {
                    Lines =
                    [
                        new PhysicalCountLineUpdateRequest
                        {
                            LineId =
                                started.Lines.Single().Id,
                            CountedQuantity = 9.5m
                        }
                    ]
                },
                UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task Complete_AppliesOnlyDifferencesAndLinksMovements()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        var decrease = await SeedProductAsync(
            db,
            "Disminuye",
            10m,
            cost: 1000m);

        var increase = await SeedProductAsync(
            db,
            "Aumenta",
            10m,
            cost: 2000m);

        var unchanged = await SeedProductAsync(
            db,
            "Igual",
            10m,
            cost: 3000m);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        var byProduct = started.Lines
            .ToDictionary(line => line.ProductId);

        await service.UpdateLinesAsync(
            started.Id,
            new PhysicalCountUpdateRequest
            {
                Lines =
                [
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = byProduct[decrease.Id].Id,
                        CountedQuantity = 8m
                    },
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = byProduct[increase.Id].Id,
                        CountedQuantity = 12m
                    },
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = byProduct[unchanged.Id].Id,
                        CountedQuantity = 10m
                    }
                ]
            },
            UserId,
            CancellationToken.None);

        var completed = await service.CompleteAsync(
            started.Id,
            UserId,
            CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(
            PhysicalCountStatus.Completed,
            completed.Status);
        Assert.NotNull(completed.CompletedAtUtc);
        Assert.Equal(UserId, completed.CompletedByUserId);

        Assert.Equal(8m, decrease.StockQuantity);
        Assert.Equal(12m, increase.StockQuantity);
        Assert.Equal(10m, unchanged.StockQuantity);

        var movements = await db.InventoryMovements
            .Where(movement =>
                movement.PhysicalCountId == started.Id)
            .OrderBy(movement => movement.ProductId)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.All(
            movements,
            movement =>
                Assert.Equal(
                    InventoryAdjustmentReason.PhysicalCount,
                    movement.AdjustmentReason));

        Assert.Contains(
            movements,
            movement =>
                movement.ProductId == decrease.Id &&
                movement.QuantityChange == -2m);

        Assert.Contains(
            movements,
            movement =>
                movement.ProductId == increase.Id &&
                movement.QuantityChange == 2m);

        var decreaseLayer = await db.InventoryCostLayers
            .SingleAsync(layer =>
                layer.ProductId == decrease.Id);

        Assert.Equal(8m, decreaseLayer.RemainingQuantity);

        var increaseLayers = await db.InventoryCostLayers
            .Where(layer =>
                layer.ProductId == increase.Id)
            .OrderBy(layer => layer.Id)
            .ToListAsync();

        Assert.Equal(2, increaseLayers.Count);
        Assert.Equal(10m, increaseLayers[0].RemainingQuantity);
        Assert.Equal(2m, increaseLayers[1].RemainingQuantity);
        Assert.Null(increaseLayers[1].UnitCost);
    }

    [Fact]
    public async Task Complete_WithUncountedLine_ThrowsWithoutCompleting()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        await SeedProductAsync(
            db,
            "Pendiente",
            10m);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.CompleteAsync(
                started.Id,
                UserId,
                CancellationToken.None));

        var persisted = await db.InventoryPhysicalCounts
            .SingleAsync(count => count.Id == started.Id);

        Assert.Equal(
            PhysicalCountStatus.Draft,
            persisted.Status);
    }

    [Fact]
    public async Task Complete_WhenStockChangedAfterSnapshot_ThrowsBeforeAdjustment()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        var first = await SeedProductAsync(
            db,
            "Primero",
            10m);

        var stale = await SeedProductAsync(
            db,
            "Cambió",
            10m);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        var byProduct = started.Lines
            .ToDictionary(line => line.ProductId);

        await service.UpdateLinesAsync(
            started.Id,
            new PhysicalCountUpdateRequest
            {
                Lines =
                [
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = byProduct[first.Id].Id,
                        CountedQuantity = 8m
                    },
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = byProduct[stale.Id].Id,
                        CountedQuantity = 9m
                    }
                ]
            },
            UserId,
            CancellationToken.None);

        stale.StockQuantity = 9m;

        var staleLayer = await db.InventoryCostLayers
            .SingleAsync(layer =>
                layer.ProductId == stale.Id);

        staleLayer.RemainingQuantity = 9m;

        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<
            InventoryPhysicalCountConflictException>(
            () => service.CompleteAsync(
                started.Id,
                UserId,
                CancellationToken.None));

        Assert.Equal(10m, first.StockQuantity);

        Assert.False(
            await db.InventoryMovements.AnyAsync(
                movement =>
                    movement.PhysicalCountId ==
                    started.Id));

        var persisted = await db.InventoryPhysicalCounts
            .SingleAsync(count =>
                count.Id == started.Id);

        Assert.Equal(
            PhysicalCountStatus.Draft,
            persisted.Status);
    }

    [Fact]
    public async Task CompletedCount_CannotBeUpdatedOrCompletedAgain()
    {
        await using var db = CreateDb();
        await EnsureUserAsync(db);

        await SeedProductAsync(
            db,
            "Producto",
            10m);

        var service = CreateService(db);

        var started = await service.StartAsync(
            new PhysicalCountCreateRequest(),
            UserId,
            CancellationToken.None);

        var line = started.Lines.Single();

        await service.UpdateLinesAsync(
            started.Id,
            new PhysicalCountUpdateRequest
            {
                Lines =
                [
                    new PhysicalCountLineUpdateRequest
                    {
                        LineId = line.Id,
                        CountedQuantity = 10m
                    }
                ]
            },
            UserId,
            CancellationToken.None);

        await service.CompleteAsync(
            started.Id,
            UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.UpdateLinesAsync(
                started.Id,
                new PhysicalCountUpdateRequest
                {
                    Lines =
                    [
                        new PhysicalCountLineUpdateRequest
                        {
                            LineId = line.Id,
                            CountedQuantity = 9m
                        }
                    ]
                },
                UserId,
                CancellationToken.None));

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.CompleteAsync(
                started.Id,
                UserId,
                CancellationToken.None));
    }

    private static InventoryPhysicalCountService CreateService(
        RevestikDbContext db)
    {
        var inventoryService =
            new InventoryService(db);

        return new InventoryPhysicalCountService(
            db,
            inventoryService);
    }

    private static RevestikDbContext CreateDb() =>
        new(
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"physical-count-{Guid.NewGuid()}")
                .Options);

    private static async Task EnsureUserAsync(
        RevestikDbContext db)
    {
        if (await db.Users.AnyAsync(
                user => user.Id == UserId))
        {
            return;
        }

        db.Users.Add(
            new ApplicationUser
            {
                Id = UserId,
                UserName =
                    "physical-count@example.com",
                NormalizedUserName =
                    "PHYSICAL-COUNT@EXAMPLE.COM",
                Email =
                    "physical-count@example.com",
                NormalizedEmail =
                    "PHYSICAL-COUNT@EXAMPLE.COM",
                EmailConfirmed = true,
                DisplayName =
                    "Physical Count User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    private static async Task<Product> SeedProductAsync(
        RevestikDbContext db,
        string name,
        decimal stock,
        decimal cost = 1000m,
        bool requiresWholeUnits = true)
    {
        var category = new ProductCategory
        {
            Name = $"Categoría {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = $"Caja {Guid.NewGuid():N}",
            Symbol = $"c{Guid.NewGuid():N}"[..10],
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = name,
            Description = "Fixture conteo físico.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits =
                requiresWholeUnits,
            SalePrice = 2000m,
            CurrentCost = cost,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        var inventoryService =
            new InventoryService(db);

        await inventoryService.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = stock,
                Notes = "Inventario inicial para conteo físico."
            },
            UserId,
            CancellationToken.None);

        return product;
    }
}