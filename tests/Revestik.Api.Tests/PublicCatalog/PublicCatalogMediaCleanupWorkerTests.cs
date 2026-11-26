using Microsoft.Extensions.Logging.Abstractions;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogMediaCleanupWorkerTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "revestik-cleanup-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanupWaitsForPublicationAndOnlyRemovesExpiredPendingFiles()
    {
        var storage = new PublicCatalogMediaStorage(root);
        using var worker = new PublicCatalogMediaCleanupWorker(
            storage, NullLogger<PublicCatalogMediaCleanupWorker>.Instance);
        var expired = CreateFile("pending", 48);
        var recent = CreateFile("pending", 1);
        var active = CreateFile("active", 48);

        Task sweep;
        using (await storage.AcquireMutationAsync(default))
        {
            sweep = worker.RunMaintenanceCycleAsync(default);
            Assert.False(sweep.IsCompleted);
            Assert.True(File.Exists(expired));
        }

        await sweep;
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(active));
    }

    private string CreateFile(string directory, int ageHours)
    {
        var folder = Path.Combine(root, "public-catalog", directory);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, [1, 2, 3]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-ageHours));
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
