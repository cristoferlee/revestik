using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Extensions;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicProformaEndpoints
{
    public static IEndpointRouteBuilder MapPublicProformaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/cart/proforma", CreateAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RevestikRateLimiting.PublicProforma)
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024))
            .WithTags("Public cart proforma");
        return app;
    }

    private static async Task<IResult> CreateAsync(
        PublicProformaRequest request, HttpContext context, RevestikDbContext db,
        PublicCatalogMediaStorage media, PublicProformaPdfService renderer, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store, no-cache";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        if (!SameOrigin(context.Request)) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (context.Request.ContentLength > 16 * 1024)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        if (request.Items is null || request.Items.Count is < 1 or > 50 ||
            request.Language is not ("es" or "en" or "de") ||
            request.Items.Any(x => x.ProductId <= 0 || !ValidQuantity(x.Quantity)) ||
            request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count)
            return Results.BadRequest(new { message = "Invalid quotation selection." });

        var items = request.Items;
        var ids = items.Select(x => x.ProductId).ToArray();
        // Browser names, unit prices and image paths are ignored completely.
        var products = await PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
            .Where(p => ids.Contains(p.Id) && db.Set<PublicCatalogImage>().Any(i =>
                i.ProductId == p.Id && i.Role == "ProductCover"))
            .Include(p => p.Translations)
            .ToListAsync(ct);
        if (products.Count != items.Count)
            return Results.Conflict(new { message = "The selection contains unavailable products. Review your cart." });
        var byId = products.ToDictionary(p => p.Id);
        var symbols = products.Where(p => !string.IsNullOrWhiteSpace(p.SalesUnit))
            .Select(p => p.SalesUnit!).Distinct().ToArray();
        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.IsActive && symbols.Contains(u.Symbol))
            .Select(u => new { u.Symbol, u.RequiresWholeQuantity })
            .ToDictionaryAsync(u => u.Symbol, ct);
        foreach (var item in items)
        {
            var symbol = byId[item.ProductId].SalesUnit;
            var whole = true;
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                if (!units.TryGetValue(symbol, out var unit))
                    return Results.Conflict(new { message = "The selling unit changed. Review your cart." });
                whole = unit.RequiresWholeQuantity;
            }
            if (whole && decimal.Truncate(item.Quantity) != item.Quantity)
                return Results.BadRequest(new { message = "The quantity is invalid for its selling unit." });
        }

        var covers = await db.Set<PublicCatalogImage>().AsNoTracking()
            .Where(i => i.ProductId.HasValue && ids.Contains(i.ProductId.Value) &&
                i.Role == "ProductCover")
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .ToListAsync(ct);
        var byCover = covers.GroupBy(i => i.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var remainingImageBudget = 8 * 1024 * 1024;
        var lines = new List<PublicProformaLine>(items.Count);
        foreach (var item in items)
        {
            var product = byId[item.ProductId];
            var name = product.Translations.FirstOrDefault(t => t.LanguageCode == request.Language)?.Name ??
                       product.Translations.FirstOrDefault(t => t.LanguageCode == "es")?.Name ??
                       product.Slug;
            byte[]? image = null;
            if (byCover.TryGetValue(item.ProductId, out var cover) &&
                cover.FileSizeBytes is > 0 and <= 800_000 &&
                cover.FileSizeBytes <= remainingImageBudget &&
                cover.ContentType is "image/png" or "image/jpeg" or "image/webp")
            {
                try
                {
                    var bytes = await media.ReadActiveAsync(cover.StorageKey, ct);
                    if (bytes.Length is > 0 and <= 800_000 && bytes.Length <= remainingImageBudget)
                    {
                        // Metadata alone cannot establish that active media still contains a
                        // supported image. Reject corrupted or mismatched cover bytes without
                        // preventing the customer from downloading a text-only quotation.
                        var inspected = PublicCatalogMediaValidation.Validate(
                            bytes, "cover.bin", "ProductCover", detectFromBytes: true);
                        if (inspected.ContentType == cover.ContentType)
                        {
                            image = bytes;
                            remainingImageBudget -= bytes.Length;
                        }
                    }
                }
                catch (FileNotFoundException) { /* still produce a legible document */ }
                catch (DirectoryNotFoundException) { /* still produce a legible document */ }
                catch (InvalidDataException) { /* corrupt or unsupported image: text fallback */ }
                catch (OverflowException) { /* invalid image dimensions: text fallback */ }
                catch (ArgumentException) { /* malformed image: text fallback */ }
            }
            lines.Add(new PublicProformaLine(product.Id, name, product.Brand,
                item.Quantity, product.SalesUnit, product.Price, product.Currency, image));
        }
        var pdf = renderer.Generate(request.Language!, lines, DateTimeOffset.UtcNow);
        var fileName = request.Language switch
        {
            "en" => "REVESTIK-Preliminary-Quotation.pdf",
            "de" => "REVESTIK-Unverbindliche-Proforma.pdf",
            _ => "REVESTIK-Proforma-Preliminar.pdf"
        };
        return Results.File(pdf, "application/pdf", fileName, enableRangeProcessing: false);
    }

    private static bool ValidQuantity(decimal quantity) =>
        quantity > 0 && quantity <= 10000m && decimal.Round(quantity, 3) == quantity;

    // Cross-site form posts must not trigger expensive anonymous PDF rendering.
    private static bool SameOrigin(HttpRequest request)
    {
        var site = request.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0 && site is not ("same-origin" or "none")) return false;
        var origin = request.Headers["Origin"].ToString();
        if (origin.Length == 0) return true;
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            uri.Scheme == request.Scheme &&
            string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
