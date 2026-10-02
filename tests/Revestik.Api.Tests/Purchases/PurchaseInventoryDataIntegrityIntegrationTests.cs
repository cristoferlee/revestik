using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseInventoryDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string UserId =
        "purchase-integrity-user";

    [Fact]
    public async Task PurchaseLine_CannotBeAppliedToInventoryTwice()
    {
        var lineId = await SeedPurchaseLineAsync();

        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            var service =
                new InventoryPurchaseReceiptService(
                    dbContext);

            await service.ReceiveAsync(
                lineId,
                10000m,
                UserId,
                CancellationToken.None);
        }

        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            var service =
                new InventoryPurchaseReceiptService(
                    dbContext);

            await Assert.ThrowsAsync<
                PurchaseInventoryAlreadyAppliedException>(
                () => service.ReceiveAsync(
                    lineId,
                    10000m,
                    UserId,
                    CancellationToken.None));
        }
    }

    [Fact]
    public async Task PurchaseLine_WithInventoryMovement_CannotBeDeleted()
    {
        var lineId = await SeedPurchaseLineAsync();

        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            var service =
                new InventoryPurchaseReceiptService(
                    dbContext);

            await service.ReceiveAsync(
                lineId,
                10000m,
                UserId,
                CancellationToken.None);
        }

        await using var deleteContext =
            sqlServerFixture.CreateDbContext();

        var line = await deleteContext.PurchaseLines
            .SingleAsync(item =>
                item.Id == lineId);

        deleteContext.PurchaseLines.Remove(line);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => deleteContext.SaveChangesAsync());
    }

    private async Task<int> SeedPurchaseLineAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        await dbContext.InventoryCostConsumptions
            .ExecuteDeleteAsync();
        await dbContext.InventoryCostLayers
            .ExecuteDeleteAsync();
        await dbContext.InventoryMovements
            .ExecuteDeleteAsync();
        await dbContext.PurchaseLines
            .ExecuteDeleteAsync();
        await dbContext.Purchases
            .ExecuteDeleteAsync();
        await dbContext.Products
            .ExecuteDeleteAsync();
        await dbContext.ProductCategories
            .ExecuteDeleteAsync();
        await dbContext.UnitsOfMeasure
            .ExecuteDeleteAsync();
        await dbContext.Suppliers
            .ExecuteDeleteAsync();

        var user =
            await dbContext.Users
                .SingleOrDefaultAsync(
                    item => item.Id == UserId);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = UserId,
                UserName =
                    "purchase-integrity@example.com",
                NormalizedUserName =
                    "PURCHASE-INTEGRITY@EXAMPLE.COM",
                Email =
                    "purchase-integrity@example.com",
                NormalizedEmail =
                    "PURCHASE-INTEGRITY@EXAMPLE.COM",
                EmailConfirmed = true,
                DisplayName =
                    "Purchase Integrity User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.IsActive = true;
        }

        var supplier = new Supplier
        {
            Name =
                $"Proveedor {Guid.NewGuid():N}",
            ContactName = "Contacto",
            PhoneNumber = "88888888",
            Email =
                $"integrity-{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var category = new ProductCategory
        {
            Name =
                $"Categoría {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name =
                $"Unidad {Guid.NewGuid():N}",
            Symbol =
                $"u{Random.Shared.Next(10000, 99999)}",
            RequiresWholeQuantity = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(
            supplier,
            category,
            unit);

        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Producto integrity",
            Description = "Producto integrity.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 9000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.Today),
            Currency = PurchaseCurrency.CRC,
            Notes = string.Empty,
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        purchase.Lines.Add(
            new PurchaseLine
            {
                ProductId = product.Id,
                Quantity = 5m,
                UnitCost = 10000m
            });

        dbContext.Purchases.Add(purchase);
        await dbContext.SaveChangesAsync();

        return purchase.Lines.Single().Id;
    }
}