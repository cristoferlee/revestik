using Microsoft.Extensions.Options;

namespace Revestik.Api.Services.Projects.Documents;

// Private storage: RootPath must be outside the web root and should be a durable volume.
public sealed class LocalProjectFileStorage(IOptions<ProjectFileStorageOptions> options) : IProjectFileStorage, IProjectFileInventory
{
    private readonly string root = ResolveRoot(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(root);
        var key = $"{Guid.NewGuid():N}";
        var path = Path.Combine(root, key);
        try
        {
            await using var destination = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            return key;
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { }
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveKey(storageKey);
        Stream source = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(source);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(ResolveKey(storageKey));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysOlderThanAsync(DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(root)) return Task.FromResult<IReadOnlyList<string>>([]);
        var keys = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Path.GetFileName(path);
            if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _)) continue;
            // Invalid files and unexpected directories are never deleted by reconciliation.
            if (File.GetLastWriteTimeUtc(path) <= cutoffUtc) keys.Add(key);
        }
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    private string ResolveKey(string key)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _))
            throw new ArgumentException("Invalid storage key.", nameof(key));
        return Path.Combine(root, key);
    }

    private static string ResolveRoot(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("ProjectFileStorage:RootPath must be an absolute private directory.");
        return Path.GetFullPath(configured);
    }
}
