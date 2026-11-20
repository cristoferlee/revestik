using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Services.PublicProjects;
using Revestik.Api.Extensions;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPublicCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var admin=app.MapGroup("/api/admin/public-catalog")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Public catalog administration");
        admin.MapGet("/categories", async (RevestikDbContext db,CancellationToken ct) =>
            Results.Ok((await db.ProductCategories.AsNoTracking().OrderBy(x=>x.PublicDisplayOrder)
                .ThenBy(x=>x.Name).ToListAsync(ct)).Select(CategoryDto)));
        // Preserve CSRF protection on the old route, but never create parallel categories.
        admin.MapPost("/categories", () => Bad("Creá las categorías en Configuración → Catálogos de productos."))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/categories/{id:int}", () => Bad("Administrá las categorías desde Configuración.")).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapGet("/subcategories",async (RevestikDbContext db,CancellationToken ct) =>
            Results.Ok((await db.ProductSubcategories.AsNoTracking().OrderBy(x=>x.CategoryId)
                .ThenBy(x=>x.PublicDisplayOrder).ToListAsync(ct)).Select(SubcategoryDto)));
        admin.MapPost("/subcategories", () => Bad("Creá las subcategorías en Configuración → Catálogos de productos."))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/subcategories/{id:int}", () => Bad("Administrá las subcategorías desde Configuración.")).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapGet("/categories/{id:int}/images", async (int id, RevestikDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Set<PublicCatalogImage>().AsNoTracking()
                .Where(i => i.CategoryId == id).OrderBy(i => i.DisplayOrder)
                .ToListAsync(ct)).Select(ImageDto)));
        admin.MapGet("/images/{id:int}/content", async (int id, RevestikDbContext db,
            PublicCatalogMediaStorage storage, CancellationToken ct) =>
        {
            var image = await db.Set<PublicCatalogImage>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
            if (image is null) return Results.NotFound();
            try { return Results.File(await storage.ReadActiveAsync(image.StorageKey, ct), image.ContentType); }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });
        admin.MapGet("/products",AdminProducts);
        admin.MapGet("/products/{id:int}",AdminProduct);
        admin.MapPost("/products",CreateProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/products/{id:int}",UpdateProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/products/{id:int}/content",UpdateProductContent).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/products/{id:int}/publish",PublishProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/products/{id:int}/unpublish",UnpublishProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapDelete("/products/{id:int}",DeleteProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/images/stage",StageImage).DisableAntiforgery().AddEndpointFilter<AntiforgeryValidationFilter>()
            .RequireRateLimiting(RevestikRateLimiting.DocumentUpload);
        admin.MapGet("/images/staged/{token}",PreviewStagedImage);
        admin.MapDelete("/images/staged/{token}",DeleteStagedImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/products/{id:int}/images",AddProductImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/categories/{id:int}/images",AddCategoryImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/images/{id:int}",EditImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapDelete("/images/{id:int}",DeleteImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        // The shared taxonomy needs no manual public/internal binding endpoints.
        var publicGroup=app.MapGroup("/api/public/catalog").AllowAnonymous().WithTags("Public catalog");
        publicGroup.MapGet("/categories",PublicCategories);
        publicGroup.MapGet("/categories/{slug}",PublicCategory);
        publicGroup.MapGet("/products",PublicProducts);
        publicGroup.MapGet("/featured-products",PublicFeaturedProducts)
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(30))
                .SetVaryByQuery("slugs").Tag("revestik-public-home"));
        publicGroup.MapGet("/products/{slug}",PublicProduct);
        publicGroup.MapGet("/images/{id:int}",GetPublicImage);
        return app;
    }

    private static string? Actor(ClaimsPrincipal principal)=>principal.FindFirstValue(ClaimTypes.NameIdentifier);
    private static IResult Bad(string message)=>Results.BadRequest(new {message});
    private static IResult Conflict(string message)=>Results.Conflict(new {message});
    private static bool Version(string input,byte[] current)
    {
        try { return Convert.FromBase64String(input).AsSpan().SequenceEqual(current); }
        catch(FormatException) { return false; }
    }
    private static string V(byte[] current)=>Convert.ToBase64String(current);
    private static bool SlugOk(string slug)=>slug.Length is >0 and <=160 && slug.All(c=>char.IsAsciiLetterLower(c)||char.IsAsciiDigit(c)||c=='-') && slug[0]!='-' && slug[^1]!='-';
    private static bool CategoryOk(CategoryWrite r)=>SlugOk(r.Slug)&&!string.IsNullOrWhiteSpace(r.NameEs)&&r.NameEs.Length<=150&&r.DisplayOrder>=0;
    private static bool SubcategoryOk(SubcategoryWrite r)=>SlugOk(r.Slug)&&!string.IsNullOrWhiteSpace(r.NameEs)&&r.NameEs.Length<=150&&r.DisplayOrder>=0;
    private static bool ProductOk(ProductWrite r)
    {
        if(r.DisplayOrder<0||r.Price<0||r.Brand?.Length>150||r.SalesUnit?.Length>40) return false;
        if(r.Price.HasValue && (r.Currency is not ("CRC" or "USD")||string.IsNullOrWhiteSpace(r.SalesUnit)))return false;
        if(!r.Price.HasValue && r.Currency is not null)return false;
        if(r.Translations is null||r.Translations.Count is <1 or >3||r.Translations.Select(x=>x.LanguageCode).Distinct().Count()!=r.Translations.Count)return false;
        if(!r.Translations.Any(x=>x.LanguageCode=="es"&&!string.IsNullOrWhiteSpace(x.Name)&&!string.IsNullOrWhiteSpace(x.Description)))return false;
        if(r.Translations.Any(x=>x.LanguageCode is not ("es" or "en" or "de")||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>200||x.Description?.Length>4000||x.SeoTitle?.Length>180||x.SeoDescription?.Length>320))return false;
        return r.SubcategoryIds is {Count: >0} && r.SubcategoryIds.Count<=30 && r.SubcategoryIds.Distinct().Count()==r.SubcategoryIds.Count;
    }
    // The sole selectable taxonomy is ProductCategories/ProductSubcategories.
    // Public-specific text lives on those same records; external response contracts remain stable.
    private static object CategoryDto(ProductCategory x) => new
    {
        x.Id, Slug = x.PublicSlug ?? MakePublicSlug(x.Name), NameEs = x.Name,
        NameEn = x.PublicNameEn, NameDe = x.PublicNameDe,
        DescriptionEs = x.PublicDescriptionEs, DescriptionEn = x.PublicDescriptionEn,
        DescriptionDe = x.PublicDescriptionDe, HeroTitleEs = x.PublicHeroTitleEs,
        HeroTitleEn = x.PublicHeroTitleEn, HeroTitleDe = x.PublicHeroTitleDe,
        HeroDescriptionEs = x.PublicHeroDescriptionEs,
        HeroDescriptionEn = x.PublicHeroDescriptionEn,
        HeroDescriptionDe = x.PublicHeroDescriptionDe,
        DisplayOrder = x.PublicDisplayOrder, x.IsActive, Version = V(x.RowVersion)
    };
    private static object SubcategoryDto(ProductSubcategory x) => new
    {
        x.Id, x.CategoryId, Slug = x.PublicSlug ?? MakePublicSlug(x.Name), NameEs = x.Name,
        NameEn = x.PublicNameEn, NameDe = x.PublicNameDe,
        DescriptionEs = x.PublicDescriptionEs, DescriptionEn = x.PublicDescriptionEn,
        DescriptionDe = x.PublicDescriptionDe,
        DisplayOrder = x.PublicDisplayOrder, x.IsActive, Version = V(x.RowVersion)
    };
    private static object ProductDto(PublicCatalogProduct x,IReadOnlyList<PublicCatalogImage> images)=>new{x.Id,x.Slug,x.Brand,x.Price,x.Currency,x.SalesUnit,x.DisplayOrder,x.IsPublished,x.IsArchived,Version=V(x.RowVersion),SubcategoryIds=x.SubcategoryLinks.Select(l=>l.SubcategoryId).ToArray(),Translations=x.Translations.Select(t=>new{t.LanguageCode,t.Name,t.Description,t.SeoTitle,t.SeoDescription}).ToArray(),Images=images.Select(ImageDto).ToArray()};
    private static object ImageDto(PublicCatalogImage x)=>new{x.Id,x.ProductId,x.CategoryId,x.Role,x.ContentType,x.Width,x.Height,x.DisplayOrder,x.AltEs,x.AltEn,x.AltDe,x.FallbackImageId,Version=V(x.RowVersion)};
    private static IQueryable<PublicCatalogProduct> ProductIncludes(RevestikDbContext db)=>db.PublicCatalogProducts.Include(p=>p.SubcategoryLinks).Include(p=>p.Translations);
    private static async Task<IResult> AdminProducts(RevestikDbContext db,int? page,int? pageSize,string? search,CancellationToken ct)
    {
        int p=Math.Clamp(page??1,1,100000),size=Math.Clamp(pageSize??20,1,50);
        var q=db.PublicCatalogProducts.AsNoTracking().Where(x => !x.IsArchived);
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Slug.Contains(search));
        return Results.Ok(new
        {
            Total = await q.CountAsync(ct),
            Items = await q.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
                .Skip((p - 1) * size).Take(size)
                .Select(x => new
                {
                    x.Id, x.Slug, x.IsPublished, x.IsArchived, x.Brand, x.Price, x.Currency, x.SalesUnit,
                    NameEs = x.Translations.Where(t => t.LanguageCode == "es").Select(t => t.Name).FirstOrDefault(),
                    NameEn = x.Translations.Where(t => t.LanguageCode == "en").Select(t => t.Name).FirstOrDefault(),
                    NameDe = x.Translations.Where(t => t.LanguageCode == "de").Select(t => t.Name).FirstOrDefault(),
                    CoverImageId = db.Set<PublicCatalogImage>()
                        .Where(i => i.ProductId == x.Id && i.Role == "ProductCover")
                        .Select(i => (int?)i.Id).FirstOrDefault()
                }).ToListAsync(ct)
        });
    }
    private static async Task<IResult> AdminProduct(int id,RevestikDbContext db,CancellationToken ct)
    {
        var x=await ProductIncludes(db).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(x is null)return Results.NotFound();
        return Results.Ok(ProductDto(x,await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct)));
    }
    // One shared category per public product; multiple subcategories of that category are allowed.
    // Inventory IsActive does not control which classifications can be used publicly.
    public static async Task<bool> ValidProductSubcategoriesAsync(
        IReadOnlyCollection<int> subcategoryIds, RevestikDbContext db, CancellationToken ct)
    {
        if (subcategoryIds is null || subcategoryIds.Count is < 1 or > 30 ||
            subcategoryIds.Distinct().Count() != subcategoryIds.Count)
            return false;

        var categoryIds = await db.ProductSubcategories.AsNoTracking()
            .Where(s => subcategoryIds.Contains(s.Id))
            .Select(s => s.CategoryId)
            .ToListAsync(ct);
        return categoryIds.Count == subcategoryIds.Count && categoryIds.Distinct().Count() == 1;
    }
    // Product prices must use the same registered units as the inventory catalog.
    private static Task<bool> ValidSalesUnit(ProductWrite r, RevestikDbContext db, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(r.SalesUnit)
            ? Task.FromResult(!r.Price.HasValue)
            : db.UnitsOfMeasure.AnyAsync(u => u.Symbol == r.SalesUnit && u.IsActive, ct);

    // Preserve SEO slugs without introducing a second classification catalog.
    private static async Task EnsurePresentationsAsync(
        RevestikDbContext db, IReadOnlyCollection<int> selectedSubcategoryIds, CancellationToken ct)
    {
        var selected = await db.ProductSubcategories.Include(s => s.Category)
            .Where(s => selectedSubcategoryIds.Contains(s.Id)).ToListAsync(ct);
        foreach (var category in selected.Select(s => s.Category).DistinctBy(x => x.Id))
        {
            if (category.PublicSlug is not null) continue;
            var baseSlug = MakePublicSlug(category.Name);
            var slug = baseSlug;
            if (await db.ProductCategories.AnyAsync(x => x.Id != category.Id && x.PublicSlug == slug, ct))
                slug = $"{baseSlug}-{category.Id}";
            category.PublicSlug = slug;
        }
        foreach (var sub in selected)
        {
            if (sub.PublicSlug is not null) continue;
            var baseSlug = MakePublicSlug(sub.Name);
            var slug = baseSlug;
            if (await db.ProductSubcategories.AnyAsync(x => x.CategoryId == sub.CategoryId &&
                x.Id != sub.Id && x.PublicSlug == slug, ct))
                slug = $"{baseSlug}-{sub.Id}";
            sub.PublicSlug = slug;
        }
    }

    private static string MakePublicSlug(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        var slug = string.Join('-', new string(chars.ToArray()).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length is > 0 and <= 140 ? slug : "categoria";
    }

    // Generate the public identity only on create. The source is the mandatory Spanish
    // display name, not the client-supplied slug; existing slugs are never reassigned.
    private static string SlugFromProductName(string name)
    {
        var normalized = name.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var slug = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) ==
                System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(char.ToLowerInvariant(c));
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
            if (slug.Length >= 120) break;
        }
        var value = slug.ToString().Trim('-');
        return value.Length == 0 ? "producto" : value;
    }

    public static async Task<string> GenerateUniqueProductSlugAsync(
        string name, RevestikDbContext db, CancellationToken ct)
    {
        var baseSlug = SlugFromProductName(name);
        for (var ordinal = 1; ordinal <= 10000; ordinal++)
        {
            var suffix = ordinal == 1 ? "" : $"-{ordinal}";
            var candidate = baseSlug[..Math.Min(baseSlug.Length, 120 - suffix.Length)].TrimEnd('-') + suffix;
            if (!await db.PublicCatalogProducts.AsNoTracking().AnyAsync(p => p.Slug == candidate, ct))
                return candidate;
        }
        throw new InvalidOperationException("No public product slug is available for this name.");
    }

    // The slug identifies the public resource. Updates may change its display name,
    // never its URL, even when an old client sends a different Slug field.
    private static void AssignProduct(PublicCatalogProduct x,ProductWrite r)
    {
        x.Brand=r.Brand;x.Price=r.Price;x.Currency=r.Currency;x.SalesUnit=r.SalesUnit;x.DisplayOrder=r.DisplayOrder;x.UpdatedAtUtc=DateTime.UtcNow;
        foreach(var existing in x.Translations.Where(t=>!r.Translations.Any(n=>n.LanguageCode==t.LanguageCode)).ToArray())x.Translations.Remove(existing);
        foreach(var t in r.Translations)
        {
            var existing=x.Translations.FirstOrDefault(a=>a.LanguageCode==t.LanguageCode);
            if(existing is null)x.Translations.Add(new PublicCatalogProductTranslation{LanguageCode=t.LanguageCode,Name=t.Name.Trim(),Description=t.Description,SeoTitle=t.SeoTitle,SeoDescription=t.SeoDescription});
            else{existing.Name=t.Name.Trim();existing.Description=t.Description;existing.SeoTitle=t.SeoTitle;existing.SeoDescription=t.SeoDescription;}
        }
        foreach(var existing in x.SubcategoryLinks.Where(l=>!r.SubcategoryIds.Contains(l.SubcategoryId)).ToArray())x.SubcategoryLinks.Remove(existing);
        foreach(var id in r.SubcategoryIds.Where(id=>!x.SubcategoryLinks.Any(l=>l.SubcategoryId==id)))x.SubcategoryLinks.Add(new PublicCatalogProductSubcategory{SubcategoryId=id});
    }
    private static async Task<IResult> CreateProduct(ProductWrite r,RevestikDbContext db,PublicCatalogMediaStorage storage,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!ProductOk(r)||!await ValidProductSubcategoriesAsync(r.SubcategoryIds,db,ct)||!await ValidSalesUnit(r,db,ct))return Bad("Invalid product, category, or unit selection.");
        // No persistent product draft. All media are temporary until the administrator
        // confirms the final create-and-publish request from Blazor.
        var pending=r.PendingImages;
        if(pending is null || pending.Count is <1 or >5 || pending.Count(i=>i.Role=="ProductCover")!=1 ||
            pending.Any(i=>i.Role is not ("ProductCover" or "ProductGallery")||i.DisplayOrder<0 ||
                i.AltEs?.Length>300||i.AltEn?.Length>300||i.AltDe?.Length>300) ||
            pending.Select(i=>i.Token).Distinct().Count()!=pending.Count)
            return Bad("Publish requires one cover and at most five valid staged images.");
        var validated=new List<(ImageStageWrite input,PublicCatalogValidatedMedia media)>();
        try
        {
            foreach(var i in pending)
                validated.Add((i,await storage.InspectPendingAsync(i.Token,i.Role,ct)));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException)
        {
            return Bad("Staged image is missing or invalid.");
        }
        var committed=new List<string>();
        try
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await EnsurePresentationsAsync(db, r.SubcategoryIds, ct);
            var productName = r.Translations.Single(t => t.LanguageCode == "es").Name;
            var slug = await GenerateUniqueProductSlugAsync(productName, db, ct);
            var x=new PublicCatalogProduct{CreatedAtUtc=DateTime.UtcNow,IsPublished=true,Slug=slug};
            AssignProduct(x,r);
            db.PublicCatalogProducts.Add(x);
            // SaveChanges obtains the new product ID within the active transaction.
            await db.SaveChangesAsync(ct);
            foreach(var (input,media) in validated)
            {
                var key=await storage.CommitAsync(input.Token,ct);
                committed.Add(key);
                db.Set<PublicCatalogImage>().Add(new PublicCatalogImage
                {
                    ProductId=x.Id,Role=input.Role,StorageKey=key,ContentType=media.ContentType,
                    Width=media.Width,Height=media.Height,FileSizeBytes=media.FileSizeBytes,
                    DisplayOrder=input.DisplayOrder,AltEs=input.AltEs,AltEn=input.AltEn,
                    AltDe=input.AltDe,CreatedAtUtc=DateTime.UtcNow
                });
            }
            SecurityAudit.Record(db,"PublicCatalogProductPublished","PublicCatalogProduct",x.Id.ToString(),Actor(user));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            foreach(var i in pending)
                try{storage.DeletePending(i.Token);}catch(IOException){}
            return Results.Created($"/api/admin/public-catalog/products/{x.Id}",new{x.Id,x.Slug,x.IsPublished,Version=V(x.RowVersion)});
        }
        catch(DbUpdateConcurrencyException)
        {
            foreach(var key in committed)try{storage.DeleteActive(key);}catch(IOException){}
            return Conflict("Catalog changed. Reload before publishing.");
        }
        catch(DbUpdateException)
        {
            foreach(var key in committed)try{storage.DeleteActive(key);}catch(IOException){}
            return Conflict("Product or image conflicts with existing data.");
        }
        catch(IOException)
        {
            foreach(var key in committed)try{storage.DeleteActive(key);}catch(IOException){}
            return Results.Problem(title:"Media storage failed.",statusCode:503);
        }
    }
    private static async Task<IResult> UpdateProduct(int id,ProductWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!ProductOk(r)||string.IsNullOrWhiteSpace(r.Version)||!await ValidProductSubcategoriesAsync(r.SubcategoryIds,db,ct)||!await ValidSalesUnit(r,db,ct))return Bad("Invalid product, category, or unit selection.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var x=await ProductIncludes(db).SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        await EnsurePresentationsAsync(db, r.SubcategoryIds, ct);
        AssignProduct(x,r);
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, x, ct);
        SecurityAudit.Record(db,"PublicCatalogProductUpdated","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}catch(DbUpdateException){return Conflict("Product update conflicts with existing data.");}
        return Results.Ok(ProductDto(x,await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct)));
    }
    // Edits product content and its gallery in one confirmed operation. A staged upload
    // is not published until all server validation and the SQL transaction succeed.
    private static async Task<IResult> UpdateProductContent(
        int id, ProductContentWrite request, RevestikDbContext db,
        PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        if (request.Product is not { } input || !ProductOk(input) ||
            string.IsNullOrWhiteSpace(input.Version) ||
            (!await ValidProductSubcategoriesAsync(input.SubcategoryIds, db, ct) || !await ValidSalesUnit(input, db, ct)))
            return Bad("Invalid product fields or subcategories.");

        var additions = request.PendingImages ?? [];
        var changes = request.ExistingImages ?? [];
        var removed = request.RemovedImageIds ?? [];
        if (additions.Count > 5 || additions.Select(i => i.Token).Distinct().Count() != additions.Count ||
            additions.Any(i => i.Role is not ("ProductCover" or "ProductGallery") ||
                i.DisplayOrder < 0 || i.AltEs?.Length > 300 || i.AltEn?.Length > 300 || i.AltDe?.Length > 300) ||
            removed.Count != removed.Distinct().Count() ||
            changes.Select(i => i.Id).Distinct().Count() != changes.Count ||
            changes.Any(i => i.DisplayOrder < 0 || string.IsNullOrWhiteSpace(i.Version) ||
                i.AltEs?.Length > 300 || i.AltEn?.Length > 300 || i.AltDe?.Length > 300))
            return Bad("Invalid gallery changes.");

        // Check every temporary image before making any durable change.
        var validated = new List<(ImageStageWrite Input, PublicCatalogValidatedMedia Media)>();
        try
        {
            foreach (var image in additions)
                validated.Add((image, await storage.InspectPendingAsync(image.Token, image.Role, ct)));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Bad("Staged image is missing or invalid.");
        }

        var committed = new List<string>();
        var discarded = new List<string>();
        var succeeded = false;
        IResult result;
        try
        {
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                var product = await ProductIncludes(db).SingleOrDefaultAsync(x => x.Id == id && !x.IsArchived, ct);
                if (product is null) return Results.NotFound();
                if (!Version(input.Version, product.RowVersion)) return Conflict("Product changed. Reload.");

                var current = await db.Set<PublicCatalogImage>().Where(i => i.ProductId == id).ToListAsync(ct);
                if (changes.Count != current.Count ||
                    changes.Any(change => !current.Any(i => i.Id == change.Id && Version(change.Version, i.RowVersion))) ||
                    removed.Any(imageId => !current.Any(i => i.Id == imageId)))
                    return Conflict("Gallery changed. Reload before saving.");

                var surviving = current.Where(i => !removed.Contains(i.Id)).ToList();
                if (surviving.Count + additions.Count is < 1 or > 5 ||
                    surviving.Count(i => i.Role == "ProductCover") + additions.Count(i => i.Role == "ProductCover") != 1)
                    return Bad("A published product requires one cover and at most five images.");

                foreach (var image in current)
                {
                    if (removed.Contains(image.Id))
                    {
                        discarded.Add(image.StorageKey);
                        db.Set<PublicCatalogImage>().Remove(image);
                        continue;
                    }
                    var update = changes.Single(c => c.Id == image.Id);
                    image.DisplayOrder = update.DisplayOrder;
                    image.AltEs = update.AltEs;
                    image.AltEn = update.AltEn;
                    image.AltDe = update.AltDe;
                }

                await EnsurePresentationsAsync(db, input.SubcategoryIds, ct);
                AssignProduct(product, input);
                await PublicProjectTagSnapshots.RefreshPublishedAsync(db, product, ct);
                // SQL Server's unique filtered cover index requires the old cover
                // to be deleted before inserting its replacement (same transaction).
                if (removed.Any(imageId => current.Any(i => i.Id == imageId && i.Role == "ProductCover")))
                    await db.SaveChangesAsync(ct);
                foreach (var (pending, media) in validated)
                {
                    var key = await storage.CommitAsync(pending.Token, ct);
                    committed.Add(key);
                    db.Set<PublicCatalogImage>().Add(new PublicCatalogImage
                    {
                        ProductId = id, Role = pending.Role, StorageKey = key,
                        ContentType = media.ContentType, Width = media.Width,
                        Height = media.Height, FileSizeBytes = media.FileSizeBytes,
                        DisplayOrder = pending.DisplayOrder, AltEs = pending.AltEs,
                        AltEn = pending.AltEn, AltDe = pending.AltDe,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                SecurityAudit.Record(db, "PublicCatalogProductUpdated", "PublicCatalogProduct", id.ToString(), Actor(user));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                succeeded = true;
                result = Results.Ok(new { product.Id, product.Slug, product.IsPublished, Version = V(product.RowVersion) });
            }
        }
        catch (DbUpdateConcurrencyException) { result = Conflict("Product or gallery changed. Reload."); }
        catch (DbUpdateException) { result = Conflict("Product or gallery conflicts with existing data."); }
        catch (IOException) { result = Results.Problem(title: "Media storage failed.", statusCode: 503); }
        finally
        {
            if (!succeeded)
                foreach (var key in committed) try { storage.DeleteActive(key); } catch (IOException) { }
        }
        if (succeeded)
        {
            foreach (var image in additions) try { storage.DeletePending(image.Token); } catch (IOException) { }
            // The database is authoritative. Failed physical cleanup is retried by the media worker.
            foreach (var key in discarded) try { storage.DeleteActive(key); } catch (IOException) { }
        }
        return result;
    }

    private static async Task<IResult> PublishProduct(int id,VersionWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var x=await ProductIncludes(db).SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        if(!x.Translations.Any(t=>t.LanguageCode=="es"&&!string.IsNullOrWhiteSpace(t.Name)&&!string.IsNullOrWhiteSpace(t.Description)))return Bad("Spanish content is required.");
        var ids=x.SubcategoryLinks.Select(l=>l.SubcategoryId).ToArray();
        if(!await ValidProductSubcategoriesAsync(ids,db,ct))
            return Bad("Product requires subcategories from one shared category.");
        if(await db.Set<PublicCatalogImage>().CountAsync(i=>i.ProductId==id&&i.Role=="ProductCover",ct)!=1)return Bad("One cover image is required.");
        x.IsPublished=true;x.UpdatedAtUtc=DateTime.UtcNow;
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, x, ct);
        SecurityAudit.Record(db,"PublicCatalogProductPublished","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}
        return Results.Ok(new{x.Id,x.IsPublished,Version=V(x.RowVersion)});
    }
    private static async Task<IResult> UnpublishProduct(int id,VersionWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        var x=await ProductIncludes(db).SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, x, ct);
        x.IsPublished=false;x.UpdatedAtUtc=DateTime.UtcNow;
        SecurityAudit.Record(db,"PublicCatalogProductUnpublished","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}
        return Results.Ok(new{x.Id,x.IsPublished,Version=V(x.RowVersion)});
    }

    public enum ProductDeletionStatus { Deleted, NotFound, Conflict }
    public sealed record ProductDeletionResult(ProductDeletionStatus Status, IReadOnlyList<string> StorageKeys);

    // All SQL references are removed atomically, including Home's manually selected
    // featured slugs. Storage is cleaned only after the commit succeeds.
    public static async Task<ProductDeletionResult> DeletePublicProductDataAsync(
        int id, string version, RevestikDbContext db, string? actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var product = await ProductIncludes(db).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return new(ProductDeletionStatus.NotFound, []);
        if (string.IsNullOrWhiteSpace(version) || !Version(version, product.RowVersion))
            return new(ProductDeletionStatus.Conflict, []);

        var images = await db.Set<PublicCatalogImage>()
            .Where(i => i.ProductId == id).ToListAsync(ct);
        var imageIds = images.Select(i => i.Id).ToArray();
        // Never remove a photograph referenced as a fallback by another record.
        if (await db.Set<PublicCatalogImage>().AnyAsync(i =>
            i.FallbackImageId.HasValue && imageIds.Contains(i.FallbackImageId.Value) &&
            i.ProductId != id, ct))
            return new(ProductDeletionStatus.Conflict, []);

        var home = await db.PublicHomeSections.SingleOrDefaultAsync(s => s.Key == "products", ct);
        if (home is not null)
        {
            var featured = JsonSerializer.Deserialize<List<string>>(home.FeaturedSlugsJson) ?? [];
            if (featured.RemoveAll(slug => string.Equals(slug, product.Slug, StringComparison.Ordinal)) > 0)
            {
                home.FeaturedSlugsJson = JsonSerializer.Serialize(featured);
                home.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        var keys = images.Select(i => i.StorageKey).ToArray();
        await PublicProjectTagSnapshots.DetachOnDeletionAsync(db, product, ct);
        db.Set<PublicCatalogImage>().RemoveRange(images);
        db.PublicCatalogProductSubcategories.RemoveRange(product.SubcategoryLinks);
        db.PublicCatalogProductTranslations.RemoveRange(product.Translations);
        db.PublicCatalogProducts.Remove(product);
        SecurityAudit.Record(db, "PublicCatalogProductDeleted", "PublicCatalogProduct", id.ToString(), actor);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(ProductDeletionStatus.Deleted, keys);
    }

    private static async Task<IResult> DeleteProduct(
        int id, string version, RevestikDbContext db, PublicCatalogMediaStorage storage,
        ClaimsPrincipal user, CancellationToken ct)
    {
        ProductDeletionResult deleted;
        try
        {
            deleted = await DeletePublicProductDataAsync(id, version, db, Actor(user), ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Product changed. Reload."); }
        catch (DbUpdateException) { return Conflict("Product is referenced by another record."); }
        if (deleted.Status == ProductDeletionStatus.NotFound) return Results.NotFound();
        if (deleted.Status == ProductDeletionStatus.Conflict)
            return Conflict("Product or images changed. Reload before deleting.");
        foreach (var key in deleted.StorageKeys)
        {
            // Orphan-cleanup worker retries any remaining file after 24h.
            try { storage.DeleteActive(key); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return Results.NoContent();
    }

    private static async Task<IResult> StageImage(HttpRequest request,PublicCatalogMediaStorage storage,CancellationToken ct)
    {
        if(!request.HasFormContentType||request.ContentLength>PublicCatalogMediaStorage.MaxBytes+65536)return Bad("Invalid multipart upload.");
        var form=await request.ReadFormAsync(ct);
        if(form.Files.Count!=1)return Bad("Exactly one image is required.");
        var file=form.Files[0];var role=form["role"].ToString();
        if(file.Length is <=0 or >PublicCatalogMediaStorage.MaxBytes)return Bad("Maximum image size is 5 MB.");
        try
        {
            await using var stream=file.OpenReadStream();
            var token=await storage.StageAsync(stream,file.FileName,role,ct);
            var info=await storage.InspectPendingAsync(token,role,ct);
            return Results.Ok(new{Token=token,info.ContentType,info.Width,info.Height,info.FrameCount});
        }
        catch(InvalidDataException ex){return Bad(ex.Message);}
    }
    private static async Task<IResult> PreviewStagedImage(string token,PublicCatalogMediaStorage storage,CancellationToken ct)
    {
        try
        {
            var bytes=await storage.ReadPendingAsync(token,ct);
            var detected=PublicCatalogMediaValidation.Validate(bytes,"media.bin","CategoryHeroPrimary",true);
            return Results.File(bytes,detected.ContentType);
        }
        catch(FileNotFoundException){return Results.NotFound();}
        catch(InvalidDataException){return Results.NotFound();}
    }
    private static IResult DeleteStagedImage(string token,PublicCatalogMediaStorage storage)
    {
        try{storage.DeletePending(token);return Results.NoContent();}catch(InvalidDataException){return Bad("Invalid token.");}
    }
    private static async Task<IResult> AddProductImage(int id,ImageAttach r,RevestikDbContext db,PublicCatalogMediaStorage storage,ClaimsPrincipal user,CancellationToken ct)
    {
        if(r.Role is not ("ProductCover" or "ProductGallery")||r.DisplayOrder<0||r.AltEs?.Length>300||r.AltEn?.Length>300||r.AltDe?.Length>300)return Bad("Invalid product image metadata.");
        PublicCatalogValidatedMedia media;
        try{media=await storage.InspectPendingAsync(r.Token,r.Role,ct);}catch(Exception ex) when(ex is IOException or InvalidDataException){return Bad("Uploaded image is missing or invalid.");}
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var product=await db.PublicCatalogProducts.SingleOrDefaultAsync(p=>p.Id==id&&!p.IsArchived,ct);
        if(product is null)return Results.NotFound();
        if(!Version(r.ParentVersion,product.RowVersion))return Conflict("Product changed. Reload.");
        if(await db.Set<PublicCatalogImage>().CountAsync(x=>x.ProductId==id,ct)>=5)return Conflict("Maximum five images per product.");
        if(r.Role=="ProductCover"&&await db.Set<PublicCatalogImage>().AnyAsync(x=>x.ProductId==id&&x.Role=="ProductCover",ct))return Conflict("Product already has a cover.");
        string key;
        try{key=await storage.CommitAsync(r.Token,ct);}catch(IOException){return Bad("Unable to commit image.");}
        var image=new PublicCatalogImage{ProductId=id,Role=r.Role,StorageKey=key,ContentType=media.ContentType,Width=media.Width,Height=media.Height,FileSizeBytes=media.FileSizeBytes,DisplayOrder=r.DisplayOrder,AltEs=r.AltEs,AltEn=r.AltEn,AltDe=r.AltDe,CreatedAtUtc=DateTime.UtcNow};
        db.Set<PublicCatalogImage>().Add(image);product.UpdatedAtUtc=DateTime.UtcNow;
        SecurityAudit.Record(db,"PublicCatalogProductImageAdded","PublicCatalogImage",null,Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){storage.DeleteActive(key);return Conflict("Product changed. Reload.");}catch(DbUpdateException){storage.DeleteActive(key);return Conflict("Image conflicts with existing data.");}
        try{storage.DeletePending(r.Token);}catch(IOException){}
        return Results.Ok(new{Image=ImageDto(image),ParentVersion=V(product.RowVersion)});
    }
    private static async Task<IResult> AddCategoryImage(int id,ImageAttach r,RevestikDbContext db,PublicCatalogMediaStorage storage,ClaimsPrincipal user,CancellationToken ct)
    {
        if(r.Role is not ("CategoryHeroPrimary" or "CategoryHeroFallback" or "CategoryGallery")||r.DisplayOrder<0||r.AltEs?.Length>300||r.AltEn?.Length>300||r.AltDe?.Length>300)return Bad("Invalid category image metadata.");
        PublicCatalogValidatedMedia media;
        try{media=await storage.InspectPendingAsync(r.Token,r.Role,ct);}catch(Exception ex) when(ex is IOException or InvalidDataException){return Bad("Uploaded image is missing or invalid.");}
        if(media.ContentType=="image/gif" && r.Role!="CategoryHeroPrimary")return Bad("GIF only permitted as main Hero.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var category=await db.ProductCategories.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(category is null)return Results.NotFound();
        if(!Version(r.ParentVersion,category.RowVersion))return Conflict("Category changed. Reload.");
        if(r.Role is "CategoryHeroPrimary" or "CategoryHeroFallback" && await db.Set<PublicCatalogImage>().AnyAsync(i=>i.CategoryId==id&&i.Role==r.Role,ct))return Conflict("Category already has an image for this role.");
        PublicCatalogImage? fallback=null;
        if(media.ContentType=="image/gif")
        {
            fallback=await db.Set<PublicCatalogImage>().SingleOrDefaultAsync(i=>i.Id==r.FallbackImageId&&i.CategoryId==id&&i.Role=="CategoryHeroFallback"&&i.ContentType!="image/gif",ct);
            if(fallback is null)return Bad("Animated GIF requires a valid static fallback from the same category.");
        }
        else if(r.FallbackImageId is not null)return Bad("Fallback references are allowed only for GIF Hero.");
        string key;
        try{key=await storage.CommitAsync(r.Token,ct);}catch(IOException){return Bad("Unable to commit image.");}
        var image=new PublicCatalogImage{CategoryId=id,Role=r.Role,StorageKey=key,ContentType=media.ContentType,Width=media.Width,Height=media.Height,FileSizeBytes=media.FileSizeBytes,DisplayOrder=r.DisplayOrder,AltEs=r.AltEs,AltEn=r.AltEn,AltDe=r.AltDe,FallbackImageId=fallback?.Id,CreatedAtUtc=DateTime.UtcNow};
        db.Set<PublicCatalogImage>().Add(image);category.UpdatedAtUtc=DateTime.UtcNow;
        SecurityAudit.Record(db,"PublicCatalogCategoryImageAdded","PublicCatalogImage",null,Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){storage.DeleteActive(key);return Conflict("Category changed. Reload.");}catch(DbUpdateException){storage.DeleteActive(key);return Conflict("Image conflicts with existing data.");}
        try{storage.DeletePending(r.Token);}catch(IOException){}
        return Results.Ok(new{Image=ImageDto(image),ParentVersion=V(category.RowVersion)});
    }
    private static async Task<IResult> EditImage(int id,ImageEdit r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(r.DisplayOrder<0||r.AltEs?.Length>300||r.AltEn?.Length>300||r.AltDe?.Length>300)return Bad("Invalid image fields.");
        var image=await db.Set<PublicCatalogImage>().SingleOrDefaultAsync(i=>i.Id==id,ct);
        if(image is null)return Results.NotFound();
        if(!Version(r.Version,image.RowVersion))return Conflict("Image changed. Reload.");
        image.DisplayOrder=r.DisplayOrder;image.AltEs=r.AltEs;image.AltEn=r.AltEn;image.AltDe=r.AltDe;
        if(image.ProductId is int pid)
        {
            var product=await db.PublicCatalogProducts.SingleAsync(p=>p.Id==pid,ct);
            product.UpdatedAtUtc=DateTime.UtcNow;
        }
        if(image.CategoryId is int cid)
        {
            var category=await db.ProductCategories.SingleAsync(p=>p.Id==cid,ct);
            category.UpdatedAtUtc=DateTime.UtcNow;
        }
        SecurityAudit.Record(db,"PublicCatalogImageUpdated","PublicCatalogImage",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Image changed. Reload.");}
        return Results.Ok(ImageDto(image));
    }
    private static async Task<IResult> DeleteImage(int id,string version,RevestikDbContext db,PublicCatalogMediaStorage storage,ClaimsPrincipal user,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var image=await db.Set<PublicCatalogImage>().SingleOrDefaultAsync(i=>i.Id==id,ct);
        if(image is null)return Results.NotFound();
        if(!Version(version,image.RowVersion))return Conflict("Image changed. Reload.");
        if(image.Role=="ProductCover"&&await db.PublicCatalogProducts.AnyAsync(p=>p.Id==image.ProductId&&p.IsPublished,ct))return Conflict("Unpublish product before removing its cover.");
        if(await db.Set<PublicCatalogImage>().AnyAsync(i=>i.FallbackImageId==id,ct))return Conflict("Image is used as animated Hero fallback.");
        if(image.ProductId is int pid)(await db.PublicCatalogProducts.SingleAsync(p=>p.Id==pid,ct)).UpdatedAtUtc=DateTime.UtcNow;
        if(image.CategoryId is int cid)(await db.ProductCategories.SingleAsync(c=>c.Id==cid,ct)).UpdatedAtUtc=DateTime.UtcNow;
        db.Set<PublicCatalogImage>().Remove(image);
        SecurityAudit.Record(db,"PublicCatalogImageDeleted","PublicCatalogImage",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Image changed. Reload.");}catch(DbUpdateException){return Conflict("Image is referenced by another record.");}
        try{storage.DeleteActive(image.StorageKey);}catch(IOException){} // orphan cleanup is retryable
        return Results.NoContent();
    }
    private static async Task<IResult> PublicCategories(RevestikDbContext db,CancellationToken ct)
    {
        var items = await PublicCatalogVisibility.VisibleCategories(db).AsNoTracking()
            .OrderBy(c => c.PublicDisplayOrder).ThenBy(c => c.Id).ToListAsync(ct);
        return Results.Ok(items.Select(CategoryDto));
    }
    private static async Task<IResult> PublicCategory(string slug,RevestikDbContext db,CancellationToken ct)
    {
        var category = await PublicCatalogVisibility.VisibleCategories(db).AsNoTracking()
            .SingleOrDefaultAsync(c => c.PublicSlug == slug, ct);
        if (category is null) return Results.NotFound();
        var subs = await PublicCatalogVisibility.VisibleSubcategories(db).AsNoTracking()
            .Where(s => s.CategoryId == category.Id)
            .OrderBy(s => s.PublicDisplayOrder).ToListAsync(ct);
        var images = await db.Set<PublicCatalogImage>().AsNoTracking()
            .Where(i => i.CategoryId == category.Id).OrderBy(i => i.DisplayOrder).ToListAsync(ct);
        return Results.Ok(new { Category = CategoryDto(category),
            Subcategories = subs.Select(SubcategoryDto), Images = images.Select(ImageDto) });
    }
    private static async Task<IResult> PublicProducts(int? page,int? pageSize,int? categoryId,int? subcategoryId,string? search,RevestikDbContext db,CancellationToken ct)
    {
        int p=Math.Clamp(page??1,1,100000),size=Math.Clamp(pageSize??20,1,50);
        var q=PublicCatalogVisibility.VisibleProducts(db).AsNoTracking();
        var visibleIds = PublicCatalogVisibility.VisibleSubcategories(db).Select(s=>s.Id);
        if(categoryId is int c)q=q.Where(x=>x.SubcategoryLinks.Any(l=>l.Subcategory.CategoryId==c&&visibleIds.Contains(l.SubcategoryId)));
        if(subcategoryId is int s)q=q.Where(x=>x.SubcategoryLinks.Any(l=>l.SubcategoryId==s&&visibleIds.Contains(l.SubcategoryId)));
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Slug.Contains(search)||x.Translations.Any(t=>t.Name.Contains(search)));
        return Results.Ok(new{Total=await q.CountAsync(ct),Page=p,PageSize=size,Items=await q.OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Id).Skip((p-1)*size).Take(size).Select(x=>new{x.Id,x.Slug,x.Brand,x.Price,x.Currency,x.SalesUnit,CoverImageId=db.Set<PublicCatalogImage>().Where(i=>i.ProductId==x.Id&&i.Role=="ProductCover").Select(i=>(int?)i.Id).FirstOrDefault(),Translations=x.Translations.Select(t=>new{t.LanguageCode,t.Name,t.Description,t.SeoTitle,t.SeoDescription})}).ToListAsync(ct)});
    }
    // One bounded public request for Home: products, translations and up to five existing
    // public catalog images per product. No operational Product/inventory tables are queried.
    private static async Task<IResult> PublicFeaturedProducts(string? slugs, RevestikDbContext db, CancellationToken ct)
    {
        if (slugs is { Length: > 1000 }) return Results.BadRequest();
        var requested = (slugs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).Take(5).ToArray();
        return Results.Ok(await ReadFeaturedPublicProductsAsync(db, requested, ct));
    }

    // Exposed for SQL integration tests: protect visibility and manual ordering in the query itself.
    public static async Task<List<object>> ReadFeaturedPublicProductsAsync(
        RevestikDbContext db, IReadOnlyList<string> requestedSlugs, CancellationToken ct)
    {
        var selected = requestedSlugs.Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal).Take(5).ToArray();
        var query = PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
            .Where(p => db.Set<PublicCatalogImage>().Any(i => i.ProductId == p.Id && i.Role == "ProductCover"))
            .Include(p => p.Translations)
            .Include(p => p.SubcategoryLinks)
            .AsSplitQuery();

        List<PublicCatalogProduct> products;
        if (selected.Length > 0)
        {
            products = await query.Where(p => selected.Contains(p.Slug)).ToListAsync(ct);
            // Preserve the editor's manually configured order; missing/unpublished entries stay absent.
            products = selected.Select(slug => products.FirstOrDefault(p => p.Slug == slug))
                .OfType<PublicCatalogProduct>().ToList();
        }
        else
        {
            products = await query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
                .Take(5).ToListAsync(ct);
        }

        if (products.Count == 0) return [];
        var ids = products.Select(p => p.Id).ToArray();
        var images = await db.Set<PublicCatalogImage>().AsNoTracking()
            .Where(i => i.ProductId.HasValue && ids.Contains(i.ProductId.Value))
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToListAsync(ct);
        var imagesByProduct = images.GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Role == "ProductCover")
                .ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id).Take(5).ToList());
        var response = new List<object>(products.Count);
        foreach (var product in products)
        {
            // A concurrent unpublish/image replacement must never turn Home into a 500.
            if (imagesByProduct.TryGetValue(product.Id, out var gallery) &&
                gallery.Any(i => i.Role == "ProductCover"))
                response.Add(ProductDto(product, gallery));
        }
        return response;
    }

    private static async Task<IResult> PublicProduct(string slug,RevestikDbContext db,CancellationToken ct)
    {
        var x=await PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
            .Include(p=>p.SubcategoryLinks).Include(p=>p.Translations)
            .SingleOrDefaultAsync(x=>x.Slug==slug,ct);
        if(x is null)return Results.NotFound();
        var images=await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==x.Id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct);
        if(!images.Any(i=>i.Role=="ProductCover"))return Results.NotFound();
        return Results.Ok(ProductDto(x,images));
    }
    private static async Task<IResult> GetPublicImage(int id,RevestikDbContext db,PublicCatalogMediaStorage storage,CancellationToken ct)
    {
        var image=await db.Set<PublicCatalogImage>().AsNoTracking().SingleOrDefaultAsync(i=>i.Id==id,ct);
        if(image is null)return Results.NotFound();
        var allowed=image.ProductId is int pid
            ? await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(p=>p.Id==pid,ct)
            : await PublicCatalogVisibility.VisibleCategories(db).AnyAsync(c=>c.Id==image.CategoryId,ct);
        if(!allowed)return Results.NotFound();
        try{return Results.File(await storage.ReadActiveAsync(image.StorageKey,ct),image.ContentType,enableRangeProcessing:false);}
        catch(FileNotFoundException){return Results.NotFound();}
    }

    public sealed record CategoryWrite(string Slug,string NameEs,string? NameEn,string? NameDe,string? DescriptionEs,string? DescriptionEn,string? DescriptionDe,string? HeroTitleEs,string? HeroTitleEn,string? HeroTitleDe,string? HeroDescriptionEs,string? HeroDescriptionEn,string? HeroDescriptionDe,int DisplayOrder,bool IsActive,string? Version);
    public sealed record SubcategoryWrite(int CategoryId,string Slug,string NameEs,string? NameEn,string? NameDe,string? DescriptionEs,string? DescriptionEn,string? DescriptionDe,int DisplayOrder,bool IsActive,string? Version);
    public sealed record TranslationWrite(string LanguageCode,string Name,string? Description,string? SeoTitle,string? SeoDescription);
    public sealed record ProductWrite(string Slug,string? Brand,decimal? Price,string? Currency,string? SalesUnit,int DisplayOrder,List<int> SubcategoryIds,List<TranslationWrite> Translations,string? Version,List<ImageStageWrite>? PendingImages);
    public sealed record ImageStageWrite(string Token,string Role,int DisplayOrder,string? AltEs,string? AltEn,string? AltDe);
    public sealed record ProductContentWrite(
        ProductWrite Product,
        List<ImageStageWrite>? PendingImages,
        List<ExistingImageWrite>? ExistingImages,
        List<int>? RemovedImageIds);
    public sealed record ExistingImageWrite(int Id, int DisplayOrder, string? AltEs, string? AltEn, string? AltDe, string Version);
    public sealed record VersionWrite(string Version);
    public sealed record ImageAttach(string Token,string Role,int DisplayOrder,string? AltEs,string? AltEn,string? AltDe,int? FallbackImageId,string ParentVersion);
    public sealed record ImageEdit(int DisplayOrder,string? AltEs,string? AltEn,string? AltDe,string Version);
}
