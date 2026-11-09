using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Services.PublicCatalog;

/// <summary>One shared taxonomy, with publication verified on every public route.</summary>
public static class PublicCatalogVisibility
{
    // IsActive belongs to the internal inventory workflow, not public publication.
    public static IQueryable<ProductCategory> VisibleCategories(RevestikDbContext db) =>
        db.ProductCategories.Where(c => c.PublicSlug != null &&
            db.PublicCatalogProducts.Any(p => p.IsPublished && !p.IsArchived &&
                p.SubcategoryLinks.Any(l => l.Subcategory.CategoryId == c.Id &&
                    l.Subcategory.PublicSlug != null)));

    public static IQueryable<ProductSubcategory> VisibleSubcategories(RevestikDbContext db) =>
        db.ProductSubcategories.Where(s =>
            s.PublicSlug != null && s.Category.PublicSlug != null &&
            VisibleCategories(db).Any(c => c.Id == s.CategoryId));

    public static IQueryable<PublicCatalogProduct> VisibleProducts(RevestikDbContext db)
    {
        var visibleIds = VisibleSubcategories(db).Select(s => s.Id);
        return db.PublicCatalogProducts.Where(p => p.IsPublished && !p.IsArchived &&
            p.SubcategoryLinks.Any(l => visibleIds.Contains(l.SubcategoryId)));
    }
}
