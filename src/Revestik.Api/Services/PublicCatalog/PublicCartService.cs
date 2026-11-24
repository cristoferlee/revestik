using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Services.PublicCatalog;

public enum PublicCartResult
{
    Success,
    Forbidden,
    NotFound,
    InvalidQuantity,
    Conflict
}

public sealed class PublicCartService(RevestikDbContext db)
{
    public const int MaxLines = 50;
    public const decimal MaxQuantity = 10000m;

    // The session claims identify the owner; recheck account activation and role in SQL.
    // A revoked or repurposed Identity account may not continue cart operations.
    public async Task<bool> IsCurrentPublicCustomerAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            !await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, ct))
            return false;
        var roles = await db.UserRoles.AsNoTracking()
            .Where(link => link.UserId == userId)
            .Join(db.Roles.AsNoTracking(), link => link.RoleId, role => role.Id,
                (_, role) => role.Name)
            .ToArrayAsync(ct);
        return roles.Length == 1 &&
            string.Equals(roles[0], RoleNames.PublicCustomer, StringComparison.Ordinal);
    }

    public async Task<PublicCartResponse> GetAsync(string userId, CancellationToken ct)
    {
        // Owner scoped in SQL, never loaded and filtered in memory.
        var lines = await db.PublicCartItems.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.ProductId)
            .ToListAsync(ct);
        if (lines.Count == 0) return new PublicCartResponse([]);

        var ids = lines.Select(item => item.ProductId).ToArray();
        var visible = await PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
            .Where(product => ids.Contains(product.Id) &&
                db.Set<PublicCatalogImage>().Any(image =>
                    image.ProductId == product.Id && image.Role == "ProductCover"))
            .Include(product => product.Translations)
            .ToListAsync(ct);
        var byId = visible.ToDictionary(product => product.Id);
        var coverIds = await db.Set<PublicCatalogImage>().AsNoTracking()
            .Where(image => image.ProductId.HasValue && ids.Contains(image.ProductId.Value) &&
                image.Role == "ProductCover")
            .Select(image => new { image.ProductId, image.Id })
            .ToListAsync(ct);
        var covers = coverIds.GroupBy(image => image.ProductId!.Value)
            .ToDictionary(group => group.Key, group => (int?)group.First().Id);

        return new PublicCartResponse(lines.Select(line =>
        {
            var available = byId.TryGetValue(line.ProductId, out var product);
            // Do not disclose names, prices or images of unpublished products.
            return new PublicCartLineResponse(
                line.ProductId, line.Quantity, Version(line.RowVersion), available,
                available ? product!.Slug : null,
                available ? product!.Brand : null,
                available ? product!.Price : null,
                available ? product!.Currency : null,
                available ? product!.SalesUnit : null,
                available && covers.TryGetValue(line.ProductId, out var cover) ? cover : null,
                available
                    ? product!.Translations.Select(translation =>
                        new PublicCartTranslationResponse(translation.LanguageCode, translation.Name)).ToArray()
                    : []);
        }).ToArray());
    }

    // Idempotent set, not additive increment. An existing line requires its current
    // version so a second tab cannot silently overwrite a previous update.
    public async Task<PublicCartResult> SetAsync(
        string userId, int productId, PublicCartQuantityWrite request, CancellationToken ct)
    {
        if (productId <= 0) return PublicCartResult.NotFound;
        if (request.Quantity <= 0 || request.Quantity > MaxQuantity ||
            decimal.Round(request.Quantity, 3) != request.Quantity)
            return PublicCartResult.InvalidQuantity;
        if (request.Version is { Length: > 0 } && !ValidEncodedVersion(request.Version))
            return PublicCartResult.Conflict;

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var product = await PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
                .Where(p => p.Id == productId &&
                    db.Set<PublicCatalogImage>().Any(image =>
                        image.ProductId == p.Id && image.Role == "ProductCover"))
                .Select(p => new { p.SalesUnit })
                .SingleOrDefaultAsync(ct);
            if (product is null) return PublicCartResult.NotFound;

            var requiresWhole = true;
            if (!string.IsNullOrWhiteSpace(product.SalesUnit))
            {
                var unit = await db.UnitsOfMeasure.AsNoTracking()
                    .Where(u => u.Symbol == product.SalesUnit && u.IsActive)
                    .Select(u => (bool?)u.RequiresWholeQuantity)
                    .FirstOrDefaultAsync(ct);
                if (unit is null) return PublicCartResult.Conflict;
                requiresWhole = unit.Value;
            }
            if (requiresWhole && decimal.Truncate(request.Quantity) != request.Quantity)
                return PublicCartResult.InvalidQuantity;

            var item = await db.PublicCartItems.SingleOrDefaultAsync(
                line => line.UserId == userId && line.ProductId == productId, ct);
            if (item is null)
            {
                if (request.Version is not null) return PublicCartResult.Conflict;
                if (await db.PublicCartItems.CountAsync(line => line.UserId == userId, ct) >= MaxLines)
                    return PublicCartResult.Conflict;
                db.PublicCartItems.Add(new PublicCartItem
                {
                    UserId = userId, ProductId = productId, Quantity = request.Quantity,
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                if (!MatchVersion(item.RowVersion, request.Version)) return PublicCartResult.Conflict;
                item.Quantity = request.Quantity;
                item.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return PublicCartResult.Success;
        }
        catch (DbUpdateConcurrencyException) { return PublicCartResult.Conflict; }
        catch (Exception e) when (IsSqlConflict(e)) { return PublicCartResult.Conflict; }
    }

    public async Task<PublicCartResult> DeleteAsync(
        string userId, int productId, string? version, CancellationToken ct)
    {
        if (productId <= 0) return PublicCartResult.NotFound;
        if (!ValidEncodedVersion(version)) return PublicCartResult.Conflict;
        try
        {
            var item = await db.PublicCartItems.SingleOrDefaultAsync(
                line => line.UserId == userId && line.ProductId == productId, ct);
            if (item is null) return PublicCartResult.NotFound;
            if (!MatchVersion(item.RowVersion, version)) return PublicCartResult.Conflict;
            db.PublicCartItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return PublicCartResult.Success;
        }
        catch (DbUpdateConcurrencyException) { return PublicCartResult.Conflict; }
        catch (Exception e) when (IsSqlConflict(e)) { return PublicCartResult.Conflict; }
    }

    private static string Version(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    private static bool MatchVersion(byte[] current, string? candidate) =>
        ValidEncodedVersion(candidate) &&
        Convert.FromBase64String(candidate!).AsSpan().SequenceEqual(current);

    private static bool ValidEncodedVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 12) return false;
        Span<byte> decoded = stackalloc byte[8];
        return Convert.TryFromBase64String(value, decoded, out var written) && written == 8;
    }

    private static bool IsSqlConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 2601 or 2627 or 547 or 1205 })
                return true;
        return false;
    }
}
