using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Extensions;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Endpoints.PublicCatalog;

internal static class PublicMediaMaintenanceEndpoints
{
    private const int ScanLimit = 5000;
    private const int CandidateLimit = 500;
    private const int DeleteLimit = 50;

    internal static void MapPublicMediaMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/public-media")
            .RequireAuthorization(PolicyNames.AdministratorOnly);
        group.MapGet("/orphans", Discover);
        group.MapPost("/orphans/delete", Delete)
            .AddEndpointFilter<AntiforgeryValidationFilter>();
    }

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider, ClaimsPrincipal user) =>
        provider.CreateProtector("PublicMediaMaintenance.v1", user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "")
            .ToTimeLimitedDataProtector();

    private static bool Eligible(FileInfo file, DateTime cutoff) =>
        file.Exists && (file.Attributes & FileAttributes.ReparsePoint) == 0
        && file.LastWriteTimeUtc <= cutoff;

    private static IQueryable<string> References(RevestikDbContext db) =>
        db.Set<PublicCatalogImage>().Select(x => x.StorageKey)
            .Concat(db.Set<PublicProjectImage>().Select(x => x.StorageKey))
            .Concat(db.PublicHomeHeroSlides.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
            .Concat(db.PublicHomeSections.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
            .Concat(db.PublicSolutionsSections.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!));

    private static async Task<IResult> Discover(PublicCatalogMediaStorage storage, RevestikDbContext db,
        IDataProtectionProvider provider, ClaimsPrincipal user, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var scan = storage.EnumerateActive().Take(ScanLimit + 1).ToArray();
        var files = scan.Take(ScanLimit)
            .Where(f => PublicCatalogMediaStorage.IsActiveKey(f.Name) && Eligible(f, cutoff))
            .Select(f => (f.Name, f.Length, f.LastWriteTimeUtc)).ToArray();
        var keys = files.Select(x => x.Name).ToArray();
        var referenced = keys.Length == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await References(db).Where(k => keys.Contains(k)).Distinct().ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var protector = Protector(provider, user);
        var candidates = files.Where(x => !referenced.Contains(x.Name)).Take(CandidateLimit + 1).ToArray();
        var items = candidates.Take(CandidateLimit).Select(x => new
        {
            id = x.Name,
            size = x.Length,
            modifiedUtc = x.LastWriteTimeUtc,
            ticket = protector.Protect(FormattableString.Invariant(
                $"{x.Name}|{x.Length}|{x.LastWriteTimeUtc.Ticks}"), TimeSpan.FromMinutes(15))
        }).ToArray();
        return Results.Ok(new { items, totalBytes = items.Sum(x => x.size),
            truncated = scan.Length > ScanLimit || candidates.Length > CandidateLimit });
    }

    internal sealed record DeleteRequest(bool Confirmed, string[]? Tickets);

    private static async Task<IResult> Delete(DeleteRequest request, PublicCatalogMediaStorage storage,
        RevestikDbContext db, IDataProtectionProvider provider, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!request.Confirmed || request.Tickets is not { Length: > 0 and <= DeleteLimit })
            return Results.BadRequest();

        var protector = Protector(provider, user);
        var selected = new Dictionary<string, (long Size, long Ticks)>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticket in request.Tickets)
        {
            if (string.IsNullOrEmpty(ticket) || ticket.Length > 1024)
                return Results.BadRequest();
            string[] fields;
            try { fields = protector.Unprotect(ticket).Split('|'); }
            catch (System.Security.Cryptography.CryptographicException) { return Results.BadRequest(); }
            if (fields.Length != 3 || !PublicCatalogMediaStorage.IsActiveKey(fields[0]) ||
                !long.TryParse(fields[1], out var size) || !long.TryParse(fields[2], out var ticks) ||
                !selected.TryAdd(fields[0], (size, ticks)))
                return Results.BadRequest();
        }

        using var lease = await storage.AcquireMutationAsync(ct);
        var keys = selected.Keys.ToArray();
        var referenced = (await References(db).Where(k => keys.Contains(k)).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var operation = Guid.NewGuid().ToString("N");
        SecurityAudit.Record(db, "PublicMediaDeletionConfirmed", "PublicMedia", operation, actor, new { count = keys.Length });
        await db.SaveChangesAsync(ct);

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var results = new List<object>();
        foreach (var (key, expected) in selected)
        {
            var status = "skipped";
            try
            {
                var file = storage.GetActiveInfo(key);
                if (!file.Exists) status = "missing";
                else if (!referenced.Contains(key) && Eligible(file, cutoff) &&
                         file.Length == expected.Size && file.LastWriteTimeUtc.Ticks == expected.Ticks)
                {
                    file.Delete();
                    status = "deleted";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status = "failed"; }
            results.Add(new { id = key, status });
            SecurityAudit.Record(db, "PublicMediaDeletionResult", "PublicMedia", key, actor, new { operation, status });
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return Results.Ok(new { results });
    }
}
