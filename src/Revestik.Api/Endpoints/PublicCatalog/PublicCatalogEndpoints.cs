using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Extensions;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPublicCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var admin=app.MapGroup("/api/admin/public-catalog")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Public catalog administration");
        admin.MapGet("/categories", async (RevestikDbContext db,CancellationToken ct) =>
            Results.Ok(await db.PublicCatalogCategories.AsNoTracking().OrderBy(x=>x.DisplayOrder).ToListAsync(ct)));
        admin.MapPost("/categories",CreateCategory).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/categories/{id:int}",UpdateCategory).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapGet("/subcategories",async (RevestikDbContext db,CancellationToken ct) =>
            Results.Ok(await db.PublicCatalogSubcategories.AsNoTracking().OrderBy(x=>x.CategoryId).ThenBy(x=>x.DisplayOrder).ToListAsync(ct)));
        admin.MapPost("/subcategories",CreateSubcategory).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/subcategories/{id:int}",UpdateSubcategory).AddEndpointFilter<AntiforgeryValidationFilter>();
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
        admin.MapPost("/products/{id:int}/publish",PublishProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/products/{id:int}/unpublish",UnpublishProduct).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/images/stage",StageImage).DisableAntiforgery().AddEndpointFilter<AntiforgeryValidationFilter>()
            .RequireRateLimiting(RevestikRateLimiting.DocumentUpload);
        admin.MapGet("/images/staged/{token}",PreviewStagedImage);
        admin.MapDelete("/images/staged/{token}",DeleteStagedImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/products/{id:int}/images",AddProductImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/categories/{id:int}/images",AddCategoryImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/images/{id:int}",EditImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapDelete("/images/{id:int}",DeleteImage).AddEndpointFilter<AntiforgeryValidationFilter>();
        var publicGroup=app.MapGroup("/api/public/catalog").AllowAnonymous().WithTags("Public catalog");
        publicGroup.MapGet("/categories",PublicCategories);
        publicGroup.MapGet("/categories/{slug}",PublicCategory);
        publicGroup.MapGet("/products",PublicProducts);
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
        if(!SlugOk(r.Slug)||r.DisplayOrder<0||r.Price<0||r.Brand?.Length>150||r.SalesUnit?.Length>40) return false;
        if(r.Price.HasValue && (r.Currency is not ("CRC" or "USD")||string.IsNullOrWhiteSpace(r.SalesUnit)))return false;
        if(!r.Price.HasValue && r.Currency is not null)return false;
        if(r.Translations is null||r.Translations.Count is <1 or >3||r.Translations.Select(x=>x.LanguageCode).Distinct().Count()!=r.Translations.Count)return false;
        if(!r.Translations.Any(x=>x.LanguageCode=="es"&&!string.IsNullOrWhiteSpace(x.Name)&&!string.IsNullOrWhiteSpace(x.Description)))return false;
        if(r.Translations.Any(x=>x.LanguageCode is not ("es" or "en" or "de")||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>200||x.Description?.Length>4000||x.SeoTitle?.Length>180||x.SeoDescription?.Length>320))return false;
        return r.SubcategoryIds is {Count: >0} && r.SubcategoryIds.Count<=30 && r.SubcategoryIds.Distinct().Count()==r.SubcategoryIds.Count;
    }
    private static object CategoryDto(PublicCatalogCategory x)=>new{x.Id,x.Slug,x.NameEs,x.NameEn,x.NameDe,x.DescriptionEs,x.DescriptionEn,x.DescriptionDe,x.HeroTitleEs,x.HeroTitleEn,x.HeroTitleDe,x.HeroDescriptionEs,x.HeroDescriptionEn,x.HeroDescriptionDe,x.DisplayOrder,x.IsActive,Version=V(x.RowVersion)};
    private static object SubcategoryDto(PublicCatalogSubcategory x)=>new{x.Id,x.CategoryId,x.Slug,x.NameEs,x.NameEn,x.NameDe,x.DescriptionEs,x.DescriptionEn,x.DescriptionDe,x.DisplayOrder,x.IsActive,Version=V(x.RowVersion)};
    private static object ProductDto(PublicCatalogProduct x,IReadOnlyList<PublicCatalogImage> images)=>new{x.Id,x.Slug,x.Brand,x.Price,x.Currency,x.SalesUnit,x.DisplayOrder,x.IsPublished,x.IsArchived,Version=V(x.RowVersion),SubcategoryIds=x.SubcategoryLinks.Select(l=>l.SubcategoryId).ToArray(),Translations=x.Translations.Select(t=>new{t.LanguageCode,t.Name,t.Description,t.SeoTitle,t.SeoDescription}).ToArray(),Images=images.Select(ImageDto).ToArray()};
    private static object ImageDto(PublicCatalogImage x)=>new{x.Id,x.ProductId,x.CategoryId,x.Role,x.ContentType,x.Width,x.Height,x.DisplayOrder,x.AltEs,x.AltEn,x.AltDe,x.FallbackImageId,Version=V(x.RowVersion)};
    private static void UpdateCategoryEntity(PublicCatalogCategory x,CategoryWrite r)
    {x.Slug=r.Slug;x.NameEs=r.NameEs.Trim();x.NameEn=r.NameEn;x.NameDe=r.NameDe;x.DescriptionEs=r.DescriptionEs;x.DescriptionEn=r.DescriptionEn;x.DescriptionDe=r.DescriptionDe;x.HeroTitleEs=r.HeroTitleEs;x.HeroTitleEn=r.HeroTitleEn;x.HeroTitleDe=r.HeroTitleDe;x.HeroDescriptionEs=r.HeroDescriptionEs;x.HeroDescriptionEn=r.HeroDescriptionEn;x.HeroDescriptionDe=r.HeroDescriptionDe;x.DisplayOrder=r.DisplayOrder;x.IsActive=r.IsActive;x.UpdatedAtUtc=DateTime.UtcNow;}
    private static void UpdateSubcategoryEntity(PublicCatalogSubcategory x,SubcategoryWrite r)
    {x.CategoryId=r.CategoryId;x.Slug=r.Slug;x.NameEs=r.NameEs.Trim();x.NameEn=r.NameEn;x.NameDe=r.NameDe;x.DescriptionEs=r.DescriptionEs;x.DescriptionEn=r.DescriptionEn;x.DescriptionDe=r.DescriptionDe;x.DisplayOrder=r.DisplayOrder;x.IsActive=r.IsActive;x.UpdatedAtUtc=DateTime.UtcNow;}

    private static async Task<IResult> CreateCategory(CategoryWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!CategoryOk(r))return Bad("Invalid category fields.");
        if(await db.PublicCatalogCategories.AnyAsync(x=>x.Slug==r.Slug,ct))return Conflict("Category slug exists.");
        var category=new PublicCatalogCategory{CreatedAtUtc=DateTime.UtcNow};UpdateCategoryEntity(category,r);
        db.PublicCatalogCategories.Add(category);
        SecurityAudit.Record(db,"PublicCatalogCategoryCreated","PublicCatalogCategory",null,Actor(user));
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return Conflict("Category conflicts with existing data.");}
        return Results.Created($"/api/admin/public-catalog/categories/{category.Id}",CategoryDto(category));
    }
    private static async Task<IResult> UpdateCategory(int id,CategoryWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!CategoryOk(r)||string.IsNullOrWhiteSpace(r.Version))return Bad("Invalid category fields/version.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var item=await db.PublicCatalogCategories.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(item is null)return Results.NotFound();
        if(!Version(r.Version,item.RowVersion))return Conflict("Category changed. Reload.");
        if(!r.IsActive && await db.PublicCatalogProducts.AnyAsync(p=>p.IsPublished && p.SubcategoryLinks.Any(l=>l.Subcategory.CategoryId==id),ct))return Conflict("Unpublish dependent products before deactivating category.");
        UpdateCategoryEntity(item,r);
        SecurityAudit.Record(db,"PublicCatalogCategoryUpdated","PublicCatalogCategory",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Category changed. Reload.");}catch(DbUpdateException){return Conflict("Category conflicts with existing data.");}
        return Results.Ok(CategoryDto(item));
    }
    private static async Task<IResult> CreateSubcategory(SubcategoryWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!SubcategoryOk(r))return Bad("Invalid subcategory fields.");
        if(!await db.PublicCatalogCategories.AnyAsync(x=>x.Id==r.CategoryId,ct))return Bad("Category does not exist.");
        var item=new PublicCatalogSubcategory{CreatedAtUtc=DateTime.UtcNow};UpdateSubcategoryEntity(item,r);
        db.PublicCatalogSubcategories.Add(item);
        SecurityAudit.Record(db,"PublicCatalogSubcategoryCreated","PublicCatalogSubcategory",null,Actor(user));
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return Conflict("Subcategory conflicts with existing data.");}
        return Results.Created($"/api/admin/public-catalog/subcategories/{item.Id}",SubcategoryDto(item));
    }
    private static async Task<IResult> UpdateSubcategory(int id,SubcategoryWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        if(!SubcategoryOk(r)||string.IsNullOrWhiteSpace(r.Version))return Bad("Invalid subcategory fields/version.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var item=await db.PublicCatalogSubcategories.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(item is null)return Results.NotFound();
        if(!Version(r.Version,item.RowVersion))return Conflict("Subcategory changed. Reload.");
        if(!await db.PublicCatalogCategories.AnyAsync(x=>x.Id==r.CategoryId,ct))return Bad("Category does not exist.");
        if((!r.IsActive || r.CategoryId!=item.CategoryId) && await db.PublicCatalogProducts.AnyAsync(p=>p.IsPublished&&p.SubcategoryLinks.Any(l=>l.SubcategoryId==id),ct))return Conflict("Unpublish dependent products before changing this subcategory.");
        UpdateSubcategoryEntity(item,r);
        SecurityAudit.Record(db,"PublicCatalogSubcategoryUpdated","PublicCatalogSubcategory",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Subcategory changed. Reload.");}catch(DbUpdateException){return Conflict("Subcategory conflicts with existing data.");}
        return Results.Ok(SubcategoryDto(item));
    }

    private static IQueryable<PublicCatalogProduct> ProductIncludes(RevestikDbContext db)=>db.PublicCatalogProducts.Include(p=>p.SubcategoryLinks).Include(p=>p.Translations);
    private static async Task<IResult> AdminProducts(RevestikDbContext db,int? page,int? pageSize,string? search,CancellationToken ct)
    {
        int p=Math.Clamp(page??1,1,100000),size=Math.Clamp(pageSize??20,1,50);
        var q=db.PublicCatalogProducts.AsNoTracking().AsQueryable();
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Slug.Contains(search));
        return Results.Ok(new{Total=await q.CountAsync(ct),Items=await q.OrderBy(x=>x.Id).Skip((p-1)*size).Take(size).Select(x=>new{x.Id,x.Slug,x.IsPublished,x.IsArchived,x.Price,x.Currency,x.SalesUnit}).ToListAsync(ct)});
    }
    private static async Task<IResult> AdminProduct(int id,RevestikDbContext db,CancellationToken ct)
    {
        var x=await ProductIncludes(db).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(x is null)return Results.NotFound();
        return Results.Ok(ProductDto(x,await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct)));
    }
    private static async Task<bool> ValidSubcategories(ProductWrite r,RevestikDbContext db,CancellationToken ct)=>
        await db.PublicCatalogSubcategories.CountAsync(s=>r.SubcategoryIds.Contains(s.Id)&&s.IsActive&&s.Category.IsActive,ct)==r.SubcategoryIds.Count;
    private static void AssignProduct(PublicCatalogProduct x,ProductWrite r)
    {
        x.Slug=r.Slug;x.Brand=r.Brand;x.Price=r.Price;x.Currency=r.Currency;x.SalesUnit=r.SalesUnit;x.DisplayOrder=r.DisplayOrder;x.UpdatedAtUtc=DateTime.UtcNow;
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
        if(!ProductOk(r)||!await ValidSubcategories(r,db,ct))return Bad("Invalid product or category selection.");
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
            var x=new PublicCatalogProduct{CreatedAtUtc=DateTime.UtcNow,IsPublished=true};
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
        if(!ProductOk(r)||string.IsNullOrWhiteSpace(r.Version)||!await ValidSubcategories(r,db,ct))return Bad("Invalid product or category selection.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var x=await ProductIncludes(db).SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        AssignProduct(x,r);
        SecurityAudit.Record(db,"PublicCatalogProductUpdated","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}catch(DbUpdateException){return Conflict("Product update conflicts with existing data.");}
        return Results.Ok(ProductDto(x,await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct)));
    }
    private static async Task<IResult> PublishProduct(int id,VersionWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var x=await ProductIncludes(db).SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        if(!x.Translations.Any(t=>t.LanguageCode=="es"&&!string.IsNullOrWhiteSpace(t.Name)&&!string.IsNullOrWhiteSpace(t.Description)))return Bad("Spanish content is required.");
        var ids=x.SubcategoryLinks.Select(l=>l.SubcategoryId).ToArray();
        var validCount=await db.PublicCatalogSubcategories.CountAsync(s=>ids.Contains(s.Id)&&s.IsActive&&s.Category.IsActive,ct);
        if(ids.Length==0 || validCount!=ids.Length)return Bad("Product requires active subcategories.");
        if(await db.Set<PublicCatalogImage>().CountAsync(i=>i.ProductId==id&&i.Role=="ProductCover",ct)!=1)return Bad("One cover image is required.");
        x.IsPublished=true;x.UpdatedAtUtc=DateTime.UtcNow;
        SecurityAudit.Record(db,"PublicCatalogProductPublished","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}
        return Results.Ok(new{x.Id,x.IsPublished,Version=V(x.RowVersion)});
    }
    private static async Task<IResult> UnpublishProduct(int id,VersionWrite r,RevestikDbContext db,ClaimsPrincipal user,CancellationToken ct)
    {
        var x=await db.PublicCatalogProducts.SingleOrDefaultAsync(x=>x.Id==id&&!x.IsArchived,ct);
        if(x is null)return Results.NotFound();
        if(!Version(r.Version,x.RowVersion))return Conflict("Product changed. Reload.");
        x.IsPublished=false;x.UpdatedAtUtc=DateTime.UtcNow;
        SecurityAudit.Record(db,"PublicCatalogProductUnpublished","PublicCatalogProduct",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Product changed. Reload.");}
        return Results.Ok(new{x.Id,x.IsPublished,Version=V(x.RowVersion)});
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
        var category=await db.PublicCatalogCategories.SingleOrDefaultAsync(x=>x.Id==id,ct);
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
            var category=await db.PublicCatalogCategories.SingleAsync(p=>p.Id==cid,ct);
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
        if(image.CategoryId is int cid)(await db.PublicCatalogCategories.SingleAsync(c=>c.Id==cid,ct)).UpdatedAtUtc=DateTime.UtcNow;
        db.Set<PublicCatalogImage>().Remove(image);
        SecurityAudit.Record(db,"PublicCatalogImageDeleted","PublicCatalogImage",id.ToString(),Actor(user));
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Image changed. Reload.");}catch(DbUpdateException){return Conflict("Image is referenced by another record.");}
        try{storage.DeleteActive(image.StorageKey);}catch(IOException){} // orphan cleanup is retryable
        return Results.NoContent();
    }
    private static async Task<IResult> PublicCategories(RevestikDbContext db,CancellationToken ct)
    {
        var items=await db.PublicCatalogCategories.AsNoTracking().Where(c=>c.IsActive).OrderBy(c=>c.DisplayOrder).ToListAsync(ct);
        return Results.Ok(items.Select(CategoryDto));
    }
    private static async Task<IResult> PublicCategory(string slug,RevestikDbContext db,CancellationToken ct)
    {
        var category=await db.PublicCatalogCategories.AsNoTracking().SingleOrDefaultAsync(c=>c.Slug==slug&&c.IsActive,ct);
        if(category is null)return Results.NotFound();
        var sub=await db.PublicCatalogSubcategories.AsNoTracking().Where(s=>s.CategoryId==category.Id&&s.IsActive).OrderBy(s=>s.DisplayOrder).ToListAsync(ct);
        var imgs=await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.CategoryId==category.Id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct);
        return Results.Ok(new{Category=CategoryDto(category),Subcategories=sub.Select(SubcategoryDto),Images=imgs.Select(ImageDto)});
    }
    private static async Task<IResult> PublicProducts(int? page,int? pageSize,int? categoryId,int? subcategoryId,string? search,RevestikDbContext db,CancellationToken ct)
    {
        int p=Math.Clamp(page??1,1,100000),size=Math.Clamp(pageSize??20,1,50);
        var q=db.PublicCatalogProducts.AsNoTracking().Where(x=>x.IsPublished&&!x.IsArchived);
        if(categoryId is int c)q=q.Where(x=>x.SubcategoryLinks.Any(l=>l.Subcategory.CategoryId==c&&l.Subcategory.IsActive&&l.Subcategory.Category.IsActive));
        if(subcategoryId is int s)q=q.Where(x=>x.SubcategoryLinks.Any(l=>l.SubcategoryId==s&&l.Subcategory.IsActive&&l.Subcategory.Category.IsActive));
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Slug.Contains(search)||x.Translations.Any(t=>t.Name.Contains(search)));
        q=q.Where(x=>x.SubcategoryLinks.Any(l=>l.Subcategory.IsActive&&l.Subcategory.Category.IsActive));
        return Results.Ok(new{Total=await q.CountAsync(ct),Page=p,PageSize=size,Items=await q.OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Id).Skip((p-1)*size).Take(size).Select(x=>new{x.Id,x.Slug,x.Brand,x.Price,x.Currency,x.SalesUnit,CoverImageId=db.Set<PublicCatalogImage>().Where(i=>i.ProductId==x.Id&&i.Role=="ProductCover").Select(i=>(int?)i.Id).FirstOrDefault(),Translations=x.Translations.Select(t=>new{t.LanguageCode,t.Name,t.Description,t.SeoTitle,t.SeoDescription})}).ToListAsync(ct)});
    }
    private static async Task<IResult> PublicProduct(string slug,RevestikDbContext db,CancellationToken ct)
    {
        var x=await ProductIncludes(db).AsNoTracking().SingleOrDefaultAsync(x=>x.Slug==slug&&x.IsPublished&&!x.IsArchived,ct);
        if(x is null || !await db.PublicCatalogSubcategories.AnyAsync(s=>x.SubcategoryLinks.Select(l=>l.SubcategoryId).Contains(s.Id)&&s.IsActive&&s.Category.IsActive,ct))return Results.NotFound();
        var images=await db.Set<PublicCatalogImage>().AsNoTracking().Where(i=>i.ProductId==x.Id).OrderBy(i=>i.DisplayOrder).ToListAsync(ct);
        if(!images.Any(i=>i.Role=="ProductCover"))return Results.NotFound();
        return Results.Ok(ProductDto(x,images));
    }
    private static async Task<IResult> GetPublicImage(int id,RevestikDbContext db,PublicCatalogMediaStorage storage,CancellationToken ct)
    {
        var image=await db.Set<PublicCatalogImage>().AsNoTracking().SingleOrDefaultAsync(i=>i.Id==id,ct);
        if(image is null)return Results.NotFound();
        var allowed=image.ProductId is int pid
            ? await db.PublicCatalogProducts.AnyAsync(p=>p.Id==pid&&p.IsPublished&&!p.IsArchived&&p.SubcategoryLinks.Any(l=>l.Subcategory.IsActive&&l.Subcategory.Category.IsActive),ct)
            : await db.PublicCatalogCategories.AnyAsync(c=>c.Id==image.CategoryId&&c.IsActive,ct);
        if(!allowed)return Results.NotFound();
        try{return Results.File(await storage.ReadActiveAsync(image.StorageKey,ct),image.ContentType,enableRangeProcessing:false);}
        catch(FileNotFoundException){return Results.NotFound();}
    }

    public sealed record CategoryWrite(string Slug,string NameEs,string? NameEn,string? NameDe,string? DescriptionEs,string? DescriptionEn,string? DescriptionDe,string? HeroTitleEs,string? HeroTitleEn,string? HeroTitleDe,string? HeroDescriptionEs,string? HeroDescriptionEn,string? HeroDescriptionDe,int DisplayOrder,bool IsActive,string? Version);
    public sealed record SubcategoryWrite(int CategoryId,string Slug,string NameEs,string? NameEn,string? NameDe,string? DescriptionEs,string? DescriptionEn,string? DescriptionDe,int DisplayOrder,bool IsActive,string? Version);
    public sealed record TranslationWrite(string LanguageCode,string Name,string? Description,string? SeoTitle,string? SeoDescription);
    public sealed record ProductWrite(string Slug,string? Brand,decimal? Price,string? Currency,string? SalesUnit,int DisplayOrder,List<int> SubcategoryIds,List<TranslationWrite> Translations,string? Version,List<ImageStageWrite>? PendingImages);
    public sealed record ImageStageWrite(string Token,string Role,int DisplayOrder,string? AltEs,string? AltEn,string? AltDe);
    public sealed record VersionWrite(string Version);
    public sealed record ImageAttach(string Token,string Role,int DisplayOrder,string? AltEs,string? AltEn,string? AltDe,int? FallbackImageId,string ParentVersion);
    public sealed record ImageEdit(int DisplayOrder,string? AltEs,string? AltEn,string? AltDe,string Version);
}
