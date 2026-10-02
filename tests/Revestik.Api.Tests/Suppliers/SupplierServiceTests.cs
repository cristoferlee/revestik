using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.Suppliers;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Tests.Suppliers;

public sealed class SupplierServiceTests
{
    [Fact]
    public async Task CreateUpdateDeactivateReactivate_PreservesSupplier()
    {
        await using var dbContext = CreateDbContext();
        var service = new SupplierService(dbContext);

        var created = await service.CreateAsync(
            CreateRequest("Proveedor Uno"),
            CancellationToken.None);

        Assert.True(created.IsActive);
        Assert.Equal("Proveedor Uno", created.Name);

        var updatedRequest = CreateRequest("Proveedor Uno Actualizado");
        updatedRequest.ContactName = "Contacto Nuevo";

        var updated = await service.UpdateAsync(
            created.Id,
            updatedRequest,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Proveedor Uno Actualizado", updated.Name);
        Assert.Equal("Contacto Nuevo", updated.ContactName);

        Assert.True(await service.DeactivateAsync(
            created.Id,
            CancellationToken.None));

        Assert.False((await service.GetByIdAsync(
            created.Id,
            CancellationToken.None))!.IsActive);

        Assert.True(await service.ReactivateAsync(
            created.Id,
            CancellationToken.None));

        Assert.True((await service.GetByIdAsync(
            created.Id,
            CancellationToken.None))!.IsActive);
    }

    [Fact]
    public async Task GetPage_SearchesByNameAndOrdersAlphabetically()
    {
        await using var dbContext = CreateDbContext();
        var service = new SupplierService(dbContext);

        await service.CreateAsync(
            CreateRequest("Zeta Cerámica"),
            CancellationToken.None);
        await service.CreateAsync(
            CreateRequest("Alfa Cerámica"),
            CancellationToken.None);
        await service.CreateAsync(
            CreateRequest("Otro Proveedor"),
            CancellationToken.None);

        var result = await service.GetPageAsync(
            new SupplierListRequest
            {
                Search = "Cerámica",
                IncludeInactive = true,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Alfa Cerámica", result.Items[0].Name);
        Assert.Equal("Zeta Cerámica", result.Items[1].Name);
    }

    [Fact]
    public async Task GetPage_ExcludesInactiveByDefault()
    {
        await using var dbContext = CreateDbContext();
        var service = new SupplierService(dbContext);

        var created = await service.CreateAsync(
            CreateRequest("Proveedor Inactivo"),
            CancellationToken.None);

        await service.DeactivateAsync(
            created.Id,
            CancellationToken.None);

        var activeOnly = await service.GetPageAsync(
            new SupplierListRequest(),
            CancellationToken.None);

        var includingInactive = await service.GetPageAsync(
            new SupplierListRequest { IncludeInactive = true },
            CancellationToken.None);

        Assert.Empty(activeOnly.Items);
        Assert.Single(includingInactive.Items);
    }

    private static RevestikDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"SupplierTests-{Guid.NewGuid()}")
            .Options);

    private static SupplierUpsertRequest CreateRequest(string name) =>
        new()
        {
            Name = name,
            ContactName = "Contacto",
            PhoneNumber = "88888888",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
}