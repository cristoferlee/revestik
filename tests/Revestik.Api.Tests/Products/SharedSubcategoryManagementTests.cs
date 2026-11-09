using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class SharedSubcategoryManagementTests
{
    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"SharedTaxonomy-{Guid.NewGuid()}").Options);

    [Fact]
    public void InvalidSubcategoryRequestsAreRejected()
    {
        var request = new ProductSubcategoryUpsertRequest { CategoryId = 0, Name = "" };
        var validation = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request,
            new ValidationContext(request), validation, true));
        Assert.Contains(validation, x => x.MemberNames.Contains(nameof(request.CategoryId)));
        Assert.Contains(validation, x => x.MemberNames.Contains(nameof(request.Name)));
    }

    [Fact]
    public async Task LifecycleKeepsCategoryAndNormalizesName()
    {
        await using var db = CreateDb();
        var service = new InventoryCatalogService(db);
        var cat = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Porcelanato" }, CancellationToken.None);
        var created = await service.CreateSubcategoryAsync(
            new ProductSubcategoryUpsertRequest { CategoryId = cat.Id, Name = "  Interior  " }, CancellationToken.None);
        Assert.Equal("Interior", created.Name);
        Assert.Equal(cat.Id, created.CategoryId);
        Assert.Single(await service.GetSubcategoriesAsync(false, CancellationToken.None));
        // EF Core InMemory does not generate SQL Server rowversion values.
        var stored = await db.ProductSubcategories.SingleAsync(x => x.Id == created.Id);
        stored.RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        await db.SaveChangesAsync();
        var current = await service.GetSubcategoryByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(current);
        Assert.True(await service.DeactivateSubcategoryAsync(created.Id, current.Version, CancellationToken.None));
        Assert.Empty(await service.GetSubcategoriesAsync(false, CancellationToken.None));
        Assert.Single(await service.GetSubcategoriesAsync(true, CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateNamesWithinSameCategoryAreRejected()
    {
        await using var db = CreateDb();
        var service = new InventoryCatalogService(db);
        var a = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Porcelanato" }, CancellationToken.None);
        var b = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Grifería" }, CancellationToken.None);
        await service.CreateSubcategoryAsync(
            new ProductSubcategoryUpsertRequest { CategoryId = a.Id, Name = "Interior" }, CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateInventoryCatalogValueException>(() =>
            service.CreateSubcategoryAsync(
                new ProductSubcategoryUpsertRequest { CategoryId = a.Id, Name = "Interior" }, CancellationToken.None));
        var other = await service.CreateSubcategoryAsync(
            new ProductSubcategoryUpsertRequest { CategoryId = b.Id, Name = "Interior" }, CancellationToken.None);
        Assert.Equal(b.Id, other.CategoryId);
    }

    [Fact]
    public async Task InactiveCategoryCannotReceiveActiveSubcategory()
    {
        await using var db = CreateDb();
        var service = new InventoryCatalogService(db);
        var cat = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Inactiva", IsActive = false }, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidProductCatalogReferenceException>(() =>
            service.CreateSubcategoryAsync(
                new ProductSubcategoryUpsertRequest { CategoryId = cat.Id, Name = "No", IsActive = true },
                CancellationToken.None));
    }

    [Fact]
    public async Task StaleVersionIsRejected()
    {
        await using var db = CreateDb();
        var service = new InventoryCatalogService(db);
        var cat = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Pisos" }, CancellationToken.None);
        var sub = await service.CreateSubcategoryAsync(
            new ProductSubcategoryUpsertRequest { CategoryId = cat.Id, Name = "Interior" }, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateSubcategoryAsync(sub.Id,
                new ProductSubcategoryUpsertRequest { CategoryId = cat.Id, Name = "Exterior" },
                "incorrect-version", CancellationToken.None));
    }
}
