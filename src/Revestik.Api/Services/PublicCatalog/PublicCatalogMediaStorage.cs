
namespace Revestik.Api.Services.PublicCatalog;

public sealed class PublicCatalogMediaStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private readonly string _root;
    private readonly string _pending;
    private readonly string _active;

    public PublicCatalogMediaStorage(IHostEnvironment env, IConfiguration config)
    {
        var root = env.IsProduction()
            ? config["RevestikStorage:RootPath"] ?? throw new InvalidOperationException("Persistent storage is required.")
            : Path.Combine(env.ContentRootPath, "App_Data");
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Media root must be absolute.");
        _root = Path.GetFullPath(Path.Combine(root, "public-catalog"));
        _pending = Path.Combine(_root, "pending");
        _active = Path.Combine(_root, "active");
    }

    public async Task<string> StageAsync(Stream input, string fileName, string role, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var n = await input.ReadAsync(chunk, ct);
            if (n == 0) break;
            if (buffer.Length + n > MaxBytes) throw new InvalidDataException("Maximum image size is 5 MB.");
            buffer.Write(chunk, 0, n);
        }
        var bytes = buffer.ToArray();
        var validated = PublicCatalogMediaValidation.Validate(bytes, fileName, role);
        Directory.CreateDirectory(_pending);
        var token = Guid.NewGuid().ToString("N");
        await using (var file = new FileStream(Path.Combine(_pending, token), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await file.WriteAsync(bytes, ct);
        return token;
    }

    public async Task<PublicCatalogValidatedMedia> InspectPendingAsync(string token, string role, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(GetPath(_pending, token), ct);
        return PublicCatalogMediaValidation.Validate(bytes, role == "CategoryHeroPrimary" ? "media.gif" : "media.bin", role, detectFromBytes: true);
    }

    public async Task<PublicCatalogValidatedMedia> InspectActiveAsync(string key, string role, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(GetPath(_active, key), ct);
        return PublicCatalogMediaValidation.Validate(bytes, "media.bin", role, detectFromBytes: true);
    }

    public async Task<string> CommitAsync(string token, CancellationToken ct)
    {
        var source = GetPath(_pending, token);
        Directory.CreateDirectory(_active);
        var key = Guid.NewGuid().ToString("N");
        var dest = GetPath(_active, key);
        await using (var from = File.OpenRead(source))
        await using (var to = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await from.CopyToAsync(to, ct);
        return key;
    }

    public Task<byte[]> ReadActiveAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(GetPath(_active, key), ct);
    public Task<byte[]> ReadPendingAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(GetPath(_pending, key), ct);
    public void DeleteActive(string key) => File.Delete(GetPath(_active, key));
    public void DeletePending(string key) => File.Delete(GetPath(_pending, key));

    public int PrunePending(TimeSpan olderThan)
    {
        if (!Directory.Exists(_pending)) return 0;
        var cutoff = DateTime.UtcNow - olderThan;
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(_pending))
        {
            if (!IsToken(Path.GetFileName(path)) || File.GetLastWriteTimeUtc(path) >= cutoff) continue;
            try { File.Delete(path); deleted++; } catch (IOException) { /* retry next sweep */ }
        }
        return deleted;
    }

    public int PruneActiveOrphans(IReadOnlySet<string> referencedKeys, TimeSpan olderThan)
    {
        if (!Directory.Exists(_active)) return 0;
        var cutoff = DateTime.UtcNow - olderThan;
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(_active))
        {
            var key = Path.GetFileName(path);
            if (!IsToken(key) || referencedKeys.Contains(key) || File.GetLastWriteTimeUtc(path) >= cutoff) continue;
            try { File.Delete(path); deleted++; } catch (IOException) { /* retry on next sweep */ }
        }
        return deleted;
    }

    private static string GetPath(string root, string key)
    {
        if (!IsToken(key)) throw new InvalidDataException("Invalid media identifier.");
        return Path.Combine(root, key);
    }
    private static bool IsToken(string key) => key.Length == 32 && Guid.TryParseExact(key, "N", out _);
}

public sealed record PublicCatalogValidatedMedia(string ContentType, int Width, int Height, long FileSizeBytes, int FrameCount);
