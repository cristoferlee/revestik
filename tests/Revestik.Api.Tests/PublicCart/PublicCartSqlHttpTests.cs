using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Endpoints.PublicCatalog;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCart;

public sealed class PublicCartSqlHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private sealed record Seed(string A, string B, int ProductId, int WholeProductId, int HiddenProductId);

    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == "PublicCustomer");
        if (role is null)
        {
            role = new IdentityRole("PublicCustomer") { NormalizedName = "PUBLICCUSTOMER" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }
        var a = "cart-a-" + Guid.NewGuid().ToString("N");
        var b = "cart-b-" + Guid.NewGuid().ToString("N");
        foreach (var id in new[] { a, b })
        {
            db.Users.Add(new ApplicationUser
            {
                Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                Email = id + "@example.test", NormalizedEmail = (id + "@example.test").ToUpperInvariant(),
                EmailConfirmed = true, DisplayName = id, IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = role.Id });
        }
        var suffix = Guid.NewGuid().ToString("N");
        var fracSymbol = "sqm-" + suffix[..8];
        var wholeSymbol = "box-" + suffix[..8];
        db.UnitsOfMeasure.AddRange(
            new UnitOfMeasure { Name = "Square meters " + suffix, Symbol = fracSymbol,
                IsActive = true, RequiresWholeQuantity = false, CreatedAtUtc = DateTime.UtcNow },
            new UnitOfMeasure { Name = "Box " + suffix, Symbol = wholeSymbol,
                IsActive = true, RequiresWholeQuantity = true, CreatedAtUtc = DateTime.UtcNow });
        var category = new ProductCategory
        {
            Name = "Cart category " + suffix, PublicSlug = "cart-cat-" + suffix,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = new ProductSubcategory
        {
            CategoryId = category.Id, Name = "Cart subcategory " + suffix,
            PublicSlug = "cart-sub-" + suffix, CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(sub);
        await db.SaveChangesAsync();
        var product = new PublicCatalogProduct
        {
            Slug = "cart-product-" + suffix, IsPublished = true, Price = 123m,
            Currency = "CRC", SalesUnit = fracSymbol, CreatedAtUtc = DateTime.UtcNow
        };
        var whole = new PublicCatalogProduct
        {
            Slug = "cart-whole-" + suffix, IsPublished = true, Price = 20m,
            Currency = "CRC", SalesUnit = wholeSymbol, CreatedAtUtc = DateTime.UtcNow
        };
        var hidden = new PublicCatalogProduct
        {
            Slug = "cart-hidden-" + suffix, IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PublicCatalogProducts.AddRange(product, whole, hidden);
        await db.SaveChangesAsync();
        foreach (var item in new[] { product, whole, hidden })
        {
            db.PublicCatalogProductSubcategories.Add(new()
            {
                ProductId = item.Id, SubcategoryId = sub.Id
            });
            db.PublicCatalogProductTranslations.Add(new()
            {
                ProductId = item.Id, LanguageCode = "es", Name = "Porcelanato de prueba",
                Description = "Descripción de prueba"
            });
            db.Set<PublicCatalogImage>().Add(new()
            {
                ProductId = item.Id, Role = "ProductCover",
                StorageKey = Guid.NewGuid().ToString("N"),
                ContentType = "image/png", Width = 100, Height = 100,
                FileSizeBytes = 200, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return new Seed(a, b, product.Id, whole.Id, hidden.Id);
    }

    private static HttpClient Client(RevestikWebApplicationFactory factory, string owner) =>
        Configure(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
            HandleCookies = true
        }), owner);

    private static HttpClient Configure(HttpClient client, string owner)
    {
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, owner);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "PublicCustomer");
        return client;
    }

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(payload);
        return payload.RequestToken;
    }

    private static async Task<HttpResponseMessage> Set(
        HttpClient client, int productId, decimal quantity, string? version, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/public/cart/items/{productId}")
        {
            Content = JsonContent.Create(new PublicCartQuantityWrite(quantity, version))
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Delete(
        HttpClient client, int productId, string version, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/public/cart/items/{productId}?version={Uri.EscapeDataString(version)}");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task OwnersAreSqlScoped_VersionsPreventStaleWrites_AndDeleteIsIsolated()
    {
        var seed = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var a = Client(factory, seed.A);
        using var b = Client(factory, seed.B);
        var tokenA = await Csrf(a);
        var tokenB = await Csrf(b);

        using (var created = await Set(a, seed.ProductId, 1.25m, null, tokenA))
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using (var other = await b.GetAsync("/api/public/cart"))
        {
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
            Assert.Empty((await other.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
        }
        using (var createdOther = await Set(b, seed.ProductId, 3m, null, tokenB))
            Assert.Equal(HttpStatusCode.OK, createdOther.StatusCode);

        PublicCartLineResponse stale;
        using (var read = await a.GetAsync("/api/public/cart"))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(read.Headers.CacheControl?.NoStore);
            stale = Assert.Single((await read.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
            Assert.True(stale.IsAvailable);
            Assert.Equal(1.25m, stale.Quantity);
            Assert.Equal("Porcelanato de prueba", Assert.Single(stale.Translations).Name);
        }
        PublicCartLineResponse updated;
        using (var change = await Set(a, seed.ProductId, 2.50m, stale.Version, tokenA))
        {
            Assert.Equal(HttpStatusCode.OK, change.StatusCode);
            updated = Assert.Single((await change.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
            Assert.NotEqual(stale.Version, updated.Version);
        }
        using (var conflict = await Set(a, seed.ProductId, 3m, stale.Version, tokenA))
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (var other = await b.GetAsync("/api/public/cart"))
            Assert.Equal(3m, Assert.Single((await other.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items).Quantity);

        using (var gone = await Delete(a, seed.ProductId, updated.Version, tokenA))
            Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode);
        using (var empty = await a.GetAsync("/api/public/cart"))
            Assert.Empty((await empty.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
        using (var remaining = await b.GetAsync("/api/public/cart"))
            Assert.Single((await remaining.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
    }

    [Fact]
    public async Task UnpublishedAndInvalidQuantitiesCannotBeAdded_AndHiddenLinesDontLeakDetails()
    {
        var seed = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory, seed.A);
        var token = await Csrf(client);
        using (var hidden = await Set(client, seed.HiddenProductId, 1m, null, token))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var fractionOfBox = await Set(client, seed.WholeProductId, 1.5m, null, token))
            Assert.Equal(HttpStatusCode.BadRequest, fractionOfBox.StatusCode);
        using (var excessPrecision = await Set(client, seed.ProductId, 1.0001m, null, token))
            Assert.Equal(HttpStatusCode.BadRequest, excessPrecision.StatusCode);
        using (var created = await Set(client, seed.ProductId, 1m, null, token))
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await using (var db = fixture.CreateDbContext())
        {
            var publicProduct = await db.PublicCatalogProducts.SingleAsync(x => x.Id == seed.ProductId);
            publicProduct.IsPublished = false;
            await db.SaveChangesAsync();
        }
        PublicCartLineResponse hiddenLine;
        using (var read = await client.GetAsync("/api/public/cart"))
        {
            hiddenLine = Assert.Single((await read.Content.ReadFromJsonAsync<PublicCartResponse>())!.Items);
            Assert.False(hiddenLine.IsAvailable);
            Assert.Null(hiddenLine.Slug);
            Assert.Null(hiddenLine.Price);
            Assert.Empty(hiddenLine.Translations);
        }
        using (var removed = await Delete(client, seed.ProductId, hiddenLine.Version, token))
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [Fact]
    public async Task PermanentProductDeletionCascadesCartRowsWithoutBlockingEditor()
    {
        var seed = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory, seed.A);
        using (var added = await Set(client, seed.ProductId, 1m, null, await Csrf(client)))
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        await using (var db = fixture.CreateDbContext())
        {
            var product = await db.PublicCatalogProducts.SingleAsync(x => x.Id == seed.ProductId);
            var result = await PublicCatalogEndpoints.DeletePublicProductDataAsync(
                product.Id, Convert.ToBase64String(product.RowVersion), db,
                "cart-test-admin", CancellationToken.None);
            Assert.Equal(PublicCatalogEndpoints.ProductDeletionStatus.Deleted, result.Status);
        }
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.PublicCartItems.AnyAsync(item =>
            item.UserId == seed.A && item.ProductId == seed.ProductId));
    }

    [Fact]
    public async Task DisabledCustomerCannotReadPersistedCart()
    {
        var seed = await SeedAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Id == seed.A);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory, seed.A);
        using var response = await client.GetAsync("/api/public/cart");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MigrationCreatedCartTableWithQuantityCheck()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, id => id.EndsWith("_AddPublicCartItems", StringComparison.Ordinal));
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'PublicCartItems'")
            .ToArrayAsync();
        Assert.Equal("PublicCartItems", Assert.Single(tables));
    }
}
