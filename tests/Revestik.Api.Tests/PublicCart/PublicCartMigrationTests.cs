using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicCartMigrationTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string PreviousMigration =
        "20261123201500_AddPublicSolutionsHeroImage";

    private const string CurrentMigration =
        "20261126193723_UseNonClusteredPublicCartPrimaryKey";

    private static async Task<string> GetPrimaryKeyTypeAsync(
        RevestikDbContext db)
    {
        return await db.Database.SqlQueryRaw<string>(
            """
            SELECT [type_desc] AS [Value]
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'PublicCartItems')
              AND is_primary_key = 1
            """).SingleAsync();
    }

    private static async Task<(string UserId, int ProductId)> SeedAsync(
        RevestikDbContext db, bool maximumLength = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var userId = maximumLength
            ? suffix.PadRight(450, 'x')
            : "migration-" + suffix;

        var user = new ApplicationUser
        {
            Id = userId,
            DisplayName = "Migration test user",
            IsActive = true
        };

        var product = new PublicCatalogProduct
        {
            Slug = "migration-product-" + suffix,
            IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.PublicCatalogProducts.Add(product);

        await db.SaveChangesAsync();

        db.PublicCartItems.Add(new PublicCartItem
        {
            UserId = userId,
            ProductId = product.Id,
            Quantity = 2.75m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (userId, product.Id);
    }

    [Fact]
    public async Task MigrationPreservesExistingRowsAndSupportsSafeRollback()
    {
        await using var db = fixture.CreateDbContext();

        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);

        Assert.Equal("CLUSTERED",
            await GetPrimaryKeyTypeAsync(db));

        var seed = await SeedAsync(db);

        var original = await db.PublicCartItems.AsNoTracking()
            .SingleAsync(x =>
                x.UserId == seed.UserId &&
                x.ProductId == seed.ProductId);

        var originalCount = await db.PublicCartItems.CountAsync();

        await migrator.MigrateAsync(CurrentMigration);

        Assert.Equal("NONCLUSTERED",
            await GetPrimaryKeyTypeAsync(db));

        var applied = await db.Database.GetAppliedMigrationsAsync();

        Assert.Contains(CurrentMigration, applied);
        Assert.Equal(59, applied.Count());

        var migrated = await db.PublicCartItems.AsNoTracking()
            .SingleAsync(x =>
                x.UserId == seed.UserId &&
                x.ProductId == seed.ProductId);

        Assert.Equal(originalCount,
            await db.PublicCartItems.CountAsync());

        Assert.Equal(original.Quantity, migrated.Quantity);
        Assert.Equal(original.CreatedAtUtc, migrated.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, migrated.UpdatedAtUtc);
        Assert.Equal(original.RowVersion, migrated.RowVersion);

        await migrator.MigrateAsync(PreviousMigration);

        Assert.Equal("CLUSTERED",
            await GetPrimaryKeyTypeAsync(db));

        applied = await db.Database.GetAppliedMigrationsAsync();

        Assert.DoesNotContain(CurrentMigration, applied);
        Assert.Equal(58, applied.Count());

        var reverted = await db.PublicCartItems.AsNoTracking()
            .SingleAsync(x =>
                x.UserId == seed.UserId &&
                x.ProductId == seed.ProductId);

        Assert.Equal(originalCount,
            await db.PublicCartItems.CountAsync());

        Assert.Equal(original.Quantity, reverted.Quantity);
        Assert.Equal(original.CreatedAtUtc, reverted.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, reverted.UpdatedAtUtc);
        Assert.Equal(original.RowVersion, reverted.RowVersion);

        // Restore the latest schema for subsequent tests.
        await migrator.MigrateAsync(CurrentMigration);

        Assert.Equal("NONCLUSTERED",
            await GetPrimaryKeyTypeAsync(db));
    }

    [Fact]
    public async Task RollbackRejectsOversizedKeysWithoutLosingData()
    {
        await using var db = fixture.CreateDbContext();

        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(CurrentMigration);

        var seed = await SeedAsync(db, maximumLength: true);

        var original = await db.PublicCartItems.AsNoTracking()
            .SingleAsync(x =>
                x.UserId == seed.UserId &&
                x.ProductId == seed.ProductId);

        var originalCount = await db.PublicCartItems.CountAsync();

        try
        {
            Assert.Equal(450, original.UserId.Length);

            var keyLength = await db.Database
                .SqlQuery<int>($"""
                    SELECT DATALENGTH([UserId])
                         + DATALENGTH([ProductId]) AS [Value]
                    FROM [PublicCartItems]
                    WHERE [UserId] = {seed.UserId}
                      AND [ProductId] = {seed.ProductId}
                    """).SingleAsync();

            Assert.Equal(904, keyLength);

            var error = await Assert.ThrowsAsync<SqlException>(
                () => migrator.MigrateAsync(PreviousMigration));

            Assert.Equal(51000, error.Number);

            Assert.Contains(
                "Cannot restore clustered cart key",
                error.Message);

            Assert.Equal("NONCLUSTERED",
                await GetPrimaryKeyTypeAsync(db));

            var applied = await db.Database.GetAppliedMigrationsAsync();

            Assert.Contains(CurrentMigration, applied);
            Assert.Equal(59, applied.Count());

            var persisted = await db.PublicCartItems.AsNoTracking()
                .SingleAsync(x =>
                    x.UserId == seed.UserId &&
                    x.ProductId == seed.ProductId);

            Assert.Equal(originalCount,
                await db.PublicCartItems.CountAsync());

            Assert.Equal(original.Quantity, persisted.Quantity);
            Assert.Equal(original.CreatedAtUtc, persisted.CreatedAtUtc);
            Assert.Equal(original.UpdatedAtUtc, persisted.UpdatedAtUtc);
            Assert.Equal(original.RowVersion, persisted.RowVersion);
        }
        finally
        {
            // Remove only synthetic data created by this test.
            await db.PublicCartItems
                .Where(x =>
                    x.UserId == seed.UserId &&
                    x.ProductId == seed.ProductId)
                .ExecuteDeleteAsync();

            await db.Users
                .Where(x => x.Id == seed.UserId)
                .ExecuteDeleteAsync();

            await db.PublicCatalogProducts
                .Where(x => x.Id == seed.ProductId)
                .ExecuteDeleteAsync();
        }
    }
}
