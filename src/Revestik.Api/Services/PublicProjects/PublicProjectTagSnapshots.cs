using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;

namespace Revestik.Api.Services.PublicProjects;

/// <summary>
/// Keep only the last PUBLIC product names on editorial tags. Hidden/unpublished product
/// updates must not leak new product names into anonymous project-detail responses.
/// All methods use the caller's DbContext and participate in its transaction.
/// </summary>
public static class PublicProjectTagSnapshots
{
    public static void CopyNames(PublicCatalogProduct product, PublicProjectImageTag tag)
    {
        var es = product.Translations.FirstOrDefault(t => t.LanguageCode == "es")?.Name;
        if (string.IsNullOrWhiteSpace(es))
            throw new InvalidOperationException("A public product must have a Spanish name.");
        tag.ProductNameSnapshotEs = es.Trim();
        tag.ProductNameSnapshotEn = product.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name?.Trim();
        tag.ProductNameSnapshotDe = product.Translations.FirstOrDefault(t => t.LanguageCode == "de")?.Name?.Trim();
    }

    public static async Task RefreshPublishedAsync(RevestikDbContext db,
        PublicCatalogProduct product, CancellationToken ct)
    {
        if (!product.IsPublished) return;
        var tags = await db.Set<PublicProjectImageTag>()
            .Where(t => t.ProductId == product.Id).ToListAsync(ct);
        foreach (var tag in tags) CopyNames(product, tag);
    }

    public static async Task DetachOnDeletionAsync(RevestikDbContext db,
        PublicCatalogProduct product, CancellationToken ct)
    {
        var tags = await db.Set<PublicProjectImageTag>()
            .Where(t => t.ProductId == product.Id).ToListAsync(ct);
        foreach (var tag in tags)
        {
            // Unpublished product edits are not authorized to alter public tag copy.
            if (product.IsPublished) CopyNames(product, tag);
            tag.ProductId = null;
            tag.Product = null;
        }
    }
}
