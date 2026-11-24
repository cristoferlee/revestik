using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCart;

// True HTTP + SQL integration. Uses Testcontainers and a separate transient media
// root; it never reads or writes the owner's local SQL Express or public media.
public sealed class PublicProformaSqlHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAFElEQVR4nGPcVGHDgA0wYRUdtBIAHHEBdvW0Ot8AAAAASUVORK5CYII=");

    private sealed record Seed(int Priced, int NoPrice, int CorruptCover, int Unpublished,
        string PricedCoverKey, string CorruptCoverKey);

    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var token = Guid.NewGuid().ToString("N");
        var unitSymbol = "sqm-" + token[..10];
        db.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Name = "Square meters " + token,
            Symbol = unitSymbol, IsActive = true, RequiresWholeQuantity = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        var category = new ProductCategory
        {
            Name = "Proforma category " + token,
            PublicSlug = "proforma-category-" + token,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = new ProductSubcategory
        {
            Name = "Proforma subcategory " + token, CategoryId = category.Id,
            PublicSlug = "proforma-subcategory-" + token,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(sub);
        await db.SaveChangesAsync();

        var priced = new PublicCatalogProduct
        {
            Slug = "proforma-priced-" + token, IsPublished = true,
            Price = 12500m, Currency = "CRC", SalesUnit = unitSymbol,
            CreatedAtUtc = DateTime.UtcNow
        };
        var noPrice = new PublicCatalogProduct
        {
            Slug = "proforma-no-price-" + token, IsPublished = true,
            SalesUnit = unitSymbol, CreatedAtUtc = DateTime.UtcNow
        };
        var corruptCover = new PublicCatalogProduct
        {
            Slug = "proforma-corrupt-" + token, IsPublished = true,
            Price = 25m, Currency = "USD", SalesUnit = unitSymbol,
            CreatedAtUtc = DateTime.UtcNow
        };
        var unpublished = new PublicCatalogProduct
        {
            Slug = "proforma-hidden-" + token, IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PublicCatalogProducts.AddRange(priced, noPrice, corruptCover, unpublished);
        await db.SaveChangesAsync();

        var pngKey = Guid.NewGuid().ToString("N");
        var brokenKey = Guid.NewGuid().ToString("N");
        foreach (var product in new[] { priced, noPrice, corruptCover, unpublished })
        {
            db.PublicCatalogProductSubcategories.Add(new()
            {
                ProductId = product.Id, SubcategoryId = sub.Id
            });
            db.PublicCatalogProductTranslations.AddRange(
                new()
                {
                    ProductId = product.Id, LanguageCode = "es",
                    Name = "Acabado " + product.Id, Description = "Producto de prueba"
                },
                new()
                {
                    ProductId = product.Id, LanguageCode = "en",
                    Name = "Finish " + product.Id, Description = "Test product"
                });
            var key = product.Id == priced.Id ? pngKey :
                product.Id == corruptCover.Id ? brokenKey : Guid.NewGuid().ToString("N");
            var length = product.Id == priced.Id ? ValidPng.Length :
                product.Id == corruptCover.Id ? 5 : 200;
            db.Set<PublicCatalogImage>().Add(new()
            {
                ProductId = product.Id, Role = "ProductCover", StorageKey = key,
                ContentType = "image/png", Width = 8, Height = 8,
                FileSizeBytes = length, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return new Seed(priced.Id, noPrice.Id, corruptCover.Id, unpublished.Id,
            pngKey, brokenKey);
    }

    private static HttpClient Client(RevestikWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

    [Fact]
    public async Task Anonymous_GeneratesLetterQuotationFromSqlAndDoesNotPersistDocuments()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var quotationCount = await db.Quotations.CountAsync();
        var cartCount = await db.PublicCartItems.CountAsync();

        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var client = Client(factory);
        var active = Path.Combine(factory.PublicMediaTestRoot, "public-catalog", "active");
        Directory.CreateDirectory(active);
        await File.WriteAllBytesAsync(Path.Combine(active, seed.PricedCoverKey), ValidPng);

        using var response = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("en", [new(seed.Priced, 2.5m), new(seed.NoPrice, 1m)]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("REVESTIK-Preliminary-Quotation.pdf",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        var pdfStructure = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/MediaBox", pdfStructure);
        // A real raster cover must be embedded, not silently replaced by text.
        Assert.Matches(@"/Subtype\s*/Image\b", pdfStructure);

        await using var verified = fixture.CreateDbContext();
        Assert.Equal(quotationCount, await verified.Quotations.CountAsync());
        Assert.Equal(cartCount, await verified.PublicCartItems.CountAsync());
    }

    [Fact]
    public async Task CorruptCoverFallsBackToTextAndGermanFilenameIsLocalized()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var seed = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var client = Client(factory);
        var active = Path.Combine(factory.PublicMediaTestRoot, "public-catalog", "active");
        Directory.CreateDirectory(active);
        await File.WriteAllBytesAsync(Path.Combine(active, seed.CorruptCoverKey),
            [0x89, 0x50, 0x4E, 0x47, 0x00]);

        using var response = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("de", [new(seed.CorruptCover, 1m)]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("REVESTIK-Unverbindliche-Proforma.pdf",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
    }

    [Fact]
    public async Task UnpublishedProductCannotBeQuoted()
    {
        var seed = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var client = Client(factory);
        using var response = await client.PostAsJsonAsync("/api/public/cart/proforma",
            new PublicProformaRequest("es", [new(seed.Unpublished, 1m)]));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
