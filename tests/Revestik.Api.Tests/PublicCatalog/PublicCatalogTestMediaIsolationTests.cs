using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogTestMediaIsolationTests
{
    [Theory]
    [InlineData("Development")]
    public async Task TestHost_UsesPrivateMediaRoot_WithoutCleanupWorker(string environmentName)
    {
        await using var factory = new RevestikWebApplicationFactory(environmentName);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var services = factory.Services;
        Assert.DoesNotContain(services.GetServices<IHostedService>(),
            hosted => hosted is PublicCatalogMediaCleanupWorker);

        var root = Path.GetFullPath(factory.PublicMediaTestRoot);
        var environment = services.GetRequiredService<IHostEnvironment>();
        var developmentMediaRoot = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "App_Data", "public-catalog"));
        var testMediaRoot = Path.GetFullPath(Path.Combine(root, "public-catalog"));

        Assert.True(Path.IsPathFullyQualified(root));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), root, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(developmentMediaRoot, testMediaRoot);

        var storage = services.GetRequiredService<PublicCatalogMediaStorage>();
        var imageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNg+M/wHwAEAQH/cetH5QAAAABJRU5ErkJggg==");

        using var stream = new MemoryStream(imageBytes);
        var pendingToken = await storage.StageAsync(
            stream, "test.png", "ProductCover", CancellationToken.None);
        string? committedKey = null;
        try
        {
            Assert.True(File.Exists(Path.Combine(testMediaRoot, "pending", pendingToken)));

            committedKey = await storage.CommitAsync(pendingToken, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(testMediaRoot, "active", committedKey)));
            Assert.Equal(imageBytes,
                await storage.ReadActiveAsync(committedKey, CancellationToken.None));
        }
        finally
        {
            // Clean only files created inside this test's own randomized temp root.
            if (committedKey is not null) storage.DeleteActive(committedKey);
            storage.DeletePending(pendingToken);
        }
    }

    [Fact]
    public async Task SeparateTestHosts_DoNotShareUploadedImages()
    {
        await using var firstFactory = new RevestikWebApplicationFactory("Development");
        await using var secondFactory = new RevestikWebApplicationFactory("Development");

        using var firstClient = firstFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var secondClient = secondFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var firstRoot = Path.GetFullPath(firstFactory.PublicMediaTestRoot);
        var secondRoot = Path.GetFullPath(secondFactory.PublicMediaTestRoot);
        Assert.NotEqual(firstRoot, secondRoot);

        var firstStorage = firstFactory.Services.GetRequiredService<PublicCatalogMediaStorage>();
        var imageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNg+M/wHwAEAQH/cetH5QAAAABJRU5ErkJggg==");

        using var stream = new MemoryStream(imageBytes);
        var token = await firstStorage.StageAsync(
            stream, "test.png", "ProductCover", CancellationToken.None);
        string? key = null;
        try
        {
            key = await firstStorage.CommitAsync(token, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(firstRoot, "public-catalog", "active", key)));
            Assert.False(File.Exists(Path.Combine(secondRoot, "public-catalog", "active", key)));
        }
        finally
        {
            if (key is not null) firstStorage.DeleteActive(key);
            firstStorage.DeletePending(token);
        }
    }
}
