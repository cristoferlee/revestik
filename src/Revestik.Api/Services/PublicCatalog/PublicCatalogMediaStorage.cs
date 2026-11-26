
namespace Revestik.Api.Services.PublicCatalog;

public sealed class PublicCatalogMediaStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private readonly string _root;
    private readonly string _pending;
    private readonly string _active;
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private readonly SemaphoreSlim _processing = new(1, 1);

    internal async Task<IDisposable> AcquireMutationAsync(CancellationToken ct)
    {
        await _mutation.WaitAsync(ct);
        return new MutationLease(_mutation);
    }

    private sealed class MutationLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }

    public PublicCatalogMediaStorage(IHostEnvironment env, IConfiguration config)
        : this(env.IsProduction()
            ? config["RevestikStorage:RootPath"] ?? throw new InvalidOperationException("Persistent storage is required.")
            : Path.Combine(env.ContentRootPath, "App_Data"))
    {
    }

    // Test hosts can supply their own root without changing normal development storage.
    public PublicCatalogMediaStorage(string root)
    {
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Media root must be absolute.");
        _root = Path.GetFullPath(Path.Combine(root, "public-catalog"));
        _pending = Path.Combine(_root, "pending");
        _active = Path.Combine(_root, "active");
    }

    public async Task<string> StageAsync(Stream input, string fileName, string role, CancellationToken ct)
    {
        if (!await _processing.WaitAsync(0, ct))
            throw new InvalidDataException("Image processing is busy. Try again.");
        try
        {
            return await StageCoreAsync(input, role, ct);
        }
        finally { _processing.Release(); }
    }

    private async Task<string> StageCoreAsync(Stream input, string role, CancellationToken ct)
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
        if (buffer.Length == 0) throw new InvalidDataException("The image is empty.");
        buffer.Position = 0;
        var maxSide = MaxSide(role);
        using var optimized = PublicImageOptimizer.Encode(buffer, maxSide);
        if (optimized.Size <= 0 || optimized.Size > MaxBytes)
            throw new InvalidDataException("The optimized image exceeds the storage limit.");
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_pending);
        var token = Guid.NewGuid().ToString("N");
        using var lease = await AcquireMutationAsync(ct);
        var path = Path.Combine(_pending, token + ".webp");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            try { optimized.SaveTo(file); }
            catch
            {
                file.Dispose();
                File.Delete(path);
                throw;
            }
        }
        return token;
    }

    public async Task<PublicCatalogValidatedMedia> InspectPendingAsync(string token, string role, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(GetPendingPath(token), ct);
        var media = PublicCatalogMediaValidation.Validate(bytes, "media.bin", role, detectFromBytes: true);
        if (File.Exists(Path.Combine(_pending, token + ".webp")) && Math.Max(media.Width, media.Height) > MaxSide(role))
            throw new InvalidDataException("Image dimensions exceed the limit for this role.");
        return media;
    }

    public async Task<PublicCatalogValidatedMedia> InspectActiveAsync(string key, string role, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(GetPath(_active, key), ct);
        return PublicCatalogMediaValidation.Validate(bytes, "media.bin", role, detectFromBytes: true);
    }

    public async Task<string> CommitAsync(string token, CancellationToken ct)
    {
        var source = GetPendingPath(token);
        Directory.CreateDirectory(_active);
        var key = Guid.NewGuid().ToString("N") + Path.GetExtension(source);
        var dest = GetPath(_active, key);
        await using (var from = File.OpenRead(source))
        await using (var to = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await from.CopyToAsync(to, ct);
        return key;
    }

    public Task<byte[]> ReadActiveAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(GetPath(_active, key), ct);
    public Task<byte[]> ReadPendingAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(GetPendingPath(key), ct);
    public void DeleteActive(string key) => File.Delete(GetPath(_active, key));
    public void DeletePending(string key) => File.Delete(GetPendingPath(key));

    internal IEnumerable<FileInfo> EnumerateActive() =>
        Directory.Exists(_active)
            ? new DirectoryInfo(_active).EnumerateFiles()
            : Enumerable.Empty<FileInfo>();

    internal FileInfo GetActiveInfo(string key) => new(GetPath(_active, key));
    internal static bool IsActiveKey(string key) => IsToken(key) ||
        (key.EndsWith(".webp", StringComparison.Ordinal) && IsToken(key[..^5]));

    public int PrunePending(TimeSpan olderThan)
    {
        if (!Directory.Exists(_pending)) return 0;
        var cutoff = DateTime.UtcNow - olderThan;
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(_pending))
        {
            if (!IsActiveKey(Path.GetFileName(path)) || File.GetLastWriteTimeUtc(path) >= cutoff) continue;
            try { File.Delete(path); deleted++; } catch (IOException) { /* retry next sweep */ }
        }
        return deleted;
    }

    private static string GetPath(string root, string key)
    {
        if (!IsActiveKey(key)) throw new InvalidDataException("Invalid media identifier.");
        return Path.Combine(root, key);
    }
    private static bool IsToken(string key) => key.Length == 32 && Guid.TryParseExact(key, "N", out _);

    private static int MaxSide(string role) => role switch
    {
        "CategoryHeroPrimary" or "CategoryHeroFallback" or "SiteHero" => 2560,
        "ProductCover" or "ProductGallery" or "CategoryGallery" => 1920,
        _ => throw new InvalidDataException("Invalid image role.")
    };

    private string GetPendingPath(string token)
    {
        if (!IsToken(token)) throw new InvalidDataException("Invalid media identifier.");
        var legacy = GetPath(_pending, token);
        var optimized = legacy + ".webp";
        return File.Exists(optimized) ? optimized : legacy;
    }
}

public sealed record PublicCatalogValidatedMedia(string ContentType, int Width, int Height, long FileSizeBytes, int FrameCount);
