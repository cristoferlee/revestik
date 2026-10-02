using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Suppliers;

public sealed class SupplierUniquenessIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();
        await dbContext.Suppliers.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DuplicateSupplierName_IsRejectedBySqlServer()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        dbContext.Suppliers.AddRange(
            CreateSupplier("Proveedor Único"),
            CreateSupplier("Proveedor Único"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    private static Supplier CreateSupplier(string name) =>
        new()
        {
            Name = name,
            ContactName = "Contacto",
            PhoneNumber = "88888888",
            Email = $"{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
}