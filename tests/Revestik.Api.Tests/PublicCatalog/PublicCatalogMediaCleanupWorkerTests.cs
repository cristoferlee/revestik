using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogMediaCleanupWorkerTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "revestik-cleanup-tests-" + Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Start =
        new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private string MediaRoot =>
        Path.Combine(root, "public-catalog");

    private string Schedule =>
        Path.Combine(
            MediaRoot, "maintenance", "next-cleanup-utc.txt");

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        public int Calls { get; private set; }

        public IServiceScope CreateScope()
        {
            Calls++;
            throw new InvalidOperationException(
                "Simulated unavailable database.");
        }
    }

    private PublicCatalogMediaCleanupWorker CreateWorker(
        IServiceScopeFactory scopes) =>
        new(
            new PublicCatalogMediaStorage(root),
            scopes,
            NullLogger<PublicCatalogMediaCleanupWorker>.Instance);

    private string CreateFile(string directory, TimeSpan age)
    {
        var path = Path.Combine(MediaRoot, directory);
        Directory.CreateDirectory(path);

        var file = Path.Combine(path, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.Subtract(age));

        return file;
    }

    [Fact]
    public async Task FirstCycle_InitializesSchedule_WithoutDatabase()
    {
        var scopes = new CountingScopeFactory();
        using var worker = CreateWorker(scopes);

        await worker.RunMaintenanceCycleAsync(Start, default);

        Assert.Equal(0, scopes.Calls);
        Assert.True(File.Exists(Schedule));

        Assert.Equal(
            Start.AddDays(30),
            DateTimeOffset.Parse(File.ReadAllText(Schedule)));
    }

    [Fact]
    public async Task RestartBeforeDueDate_DoesNotAccessDatabase()
    {
        var scopes = new CountingScopeFactory();

        using (var first = CreateWorker(scopes))
            await first.RunMaintenanceCycleAsync(Start, default);

        var original = File.ReadAllText(Schedule);

        using (var second = CreateWorker(scopes))
        {
            await second.RunMaintenanceCycleAsync(
                Start.AddDays(29), default);
        }

        Assert.Equal(0, scopes.Calls);
        Assert.Equal(original, File.ReadAllText(Schedule));
    }

    [Fact]
    public async Task PendingCleanup_RemainsIndependentOfDatabase()
    {
        var old = CreateFile("pending", TimeSpan.FromHours(48));
        var recent = CreateFile("pending", TimeSpan.FromHours(1));

        var scopes = new CountingScopeFactory();
        using var worker = CreateWorker(scopes);

        await worker.RunMaintenanceCycleAsync(Start, default);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.Equal(0, scopes.Calls);
    }

    [Fact]
    public async Task DueCycle_DatabaseFailurePreservesDurableFiles()
    {
        var active = CreateFile("active", TimeSpan.FromHours(48));

        var scopes = new CountingScopeFactory();
        using var worker = CreateWorker(scopes);

        await worker.RunMaintenanceCycleAsync(Start, default);

        var original = File.ReadAllText(Schedule);

        await worker.RunMaintenanceCycleAsync(
            Start.AddDays(30), default);

        Assert.Equal(1, scopes.Calls);
        Assert.True(File.Exists(active));
        Assert.Equal(original, File.ReadAllText(Schedule));
    }

    [Fact]
    public async Task InvalidSchedule_DoesNotAccessDatabase()
    {
        var active = CreateFile("active", TimeSpan.FromHours(48));

        var scopes = new CountingScopeFactory();
        using var worker = CreateWorker(scopes);

        await worker.RunMaintenanceCycleAsync(Start, default);

        File.WriteAllText(Schedule, "invalid-schedule");

        await worker.RunMaintenanceCycleAsync(
            Start.AddDays(30), default);

        Assert.Equal(0, scopes.Calls);
        Assert.True(File.Exists(active));
    }

    [Fact]
    public async Task ConcurrentLease_PreventsDuplicateMaintenance()
    {
        var scopes = new CountingScopeFactory();
        using var worker = CreateWorker(scopes);

        await worker.RunMaintenanceCycleAsync(Start, default);

        var lockPath = Path.Combine(
            MediaRoot, "maintenance", "cleanup.lock");

        using (var lease = new FileStream(
            lockPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            await worker.RunMaintenanceCycleAsync(
                Start.AddDays(30), default);

            Assert.Equal(0, scopes.Calls);
        }

        await worker.RunMaintenanceCycleAsync(
            Start.AddDays(30), default);

        Assert.Equal(1, scopes.Calls);
    }

    [Fact]
    public void OrphanPruning_PreservesReferencedAndRecentFiles()
    {
        var orphan = CreateFile("active", TimeSpan.FromHours(48));
        var referenced = CreateFile("active", TimeSpan.FromHours(48));
        var recent = CreateFile("active", TimeSpan.FromHours(1));

        var storage = new PublicCatalogMediaStorage(root);

        var deleted = storage.PruneActiveOrphans(
            new HashSet<string>(StringComparer.Ordinal)
            {
                Path.GetFileName(referenced)
            },
            TimeSpan.FromHours(24));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(referenced));
        Assert.True(File.Exists(recent));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
