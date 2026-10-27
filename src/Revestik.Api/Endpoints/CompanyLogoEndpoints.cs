using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.Audit;
using Revestik.Api.Models;
using Revestik.Api.Services.Company;

namespace Revestik.Api.Endpoints;

public static class CompanyLogoEndpoints
{
    public static IEndpointRouteBuilder MapCompanyLogoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/company/logo")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Company settings");
        group.MapGet("/", GetAsync).WithName("GetCompanyLogo");
        group.MapPost("/", UploadAsync)
            .DisableAntiforgery() // Existing explicit CSRF endpoint filter validates the cookie request.
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UploadCompanyLogo");
        group.MapDelete("/", RestoreAsync)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("RestoreDefaultCompanyLogo");
        return app;
    }

    private static async Task<IResult> GetAsync(RevestikDbContext db, CompanyLogoStorage storage, CancellationToken ct)
    {
        var profile = await db.Set<CompanyProfile>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==1,ct);
        if (profile is null || profile.LogoStorageKey is null)
        {
            var path=Path.Combine(AppContext.BaseDirectory,"Assets","revestik.svg");
            return File.Exists(path) ? Results.File(path,"image/svg+xml", enableRangeProcessing:false) : Results.NotFound();
        }
        try { return Results.File(await storage.ReadAsync(profile.LogoStorageKey,ct),profile.LogoContentType ?? "application/octet-stream"); }
        catch (FileNotFoundException) { return Results.Problem(title:"Logotipo no disponible en el almacenamiento privado.",statusCode:503); }
    }

    private static async Task<IResult> UploadAsync(HttpRequest request, RevestikDbContext db,
        CompanyLogoStorage storage, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!request.HasFormContentType || request.ContentLength > CompanyLogoStorage.MaxBytes + 65536)
            return Results.BadRequest("Adjuntá una imagen de hasta 2 MB.");
        var form=await request.ReadFormAsync(ct);
        if (form.Files.Count != 1) return Results.BadRequest("Seleccioná una sola imagen.");
        var file=form.Files[0];
        if (file.Length<=0 || file.Length>CompanyLogoStorage.MaxBytes)
            return Results.BadRequest("Adjuntá una imagen de hasta 2 MB.");
        var version=form["version"].ToString();
        if (!TryVersion(version,out var bytes)) return Results.BadRequest("Versión inválida.");
        var company=await db.Set<CompanyProfile>().SingleOrDefaultAsync(x=>x.Id==1,ct);
        if (company is null) return Results.Conflict(new { message="Recargá Mi empresa antes de actualizar el logotipo." });
        if (!company.RowVersion.SequenceEqual(bytes)) return Conflict();
        byte[] contents;
        await using(var stream=file.OpenReadStream())
        {
            using var memory=new MemoryStream();
            await stream.CopyToAsync(memory,ct);
            contents=memory.ToArray();
        }
        string contentType;
        try { contentType=CompanyLogoStorage.Validate(contents,file.FileName); }
        catch (InvalidDataException ex) { return Results.BadRequest(ex.Message); }
        var key=await storage.StoreAsync(contents,ct);
        company.LogoStorageKey=key;
        company.LogoContentType=contentType;
        company.UpdatedAtUtc=DateTime.UtcNow;
        company.UpdatedByUserId=principal.FindFirstValue(ClaimTypes.NameIdentifier);
        SecurityAudit.Record(db, "CompanyLogoUploaded", "Company", "1", company.UpdatedByUserId);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        return Results.Ok(new { version=Convert.ToBase64String(company.RowVersion) });
    }

    private static async Task<IResult> RestoreAsync(HttpRequest request, RevestikDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!TryVersion(request.Headers["X-Company-Version"].ToString(),out var bytes))
            return Results.BadRequest("Versión inválida.");
        var company=await db.Set<CompanyProfile>().SingleOrDefaultAsync(x=>x.Id==1,ct);
        if (company is null) return Results.NotFound();
        if (!company.RowVersion.SequenceEqual(bytes)) return Conflict();
        var hadLogo = company.LogoStorageKey is not null;
        company.LogoStorageKey=null;
        company.LogoContentType=null;
        company.UpdatedAtUtc=DateTime.UtcNow;
        company.UpdatedByUserId=principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (hadLogo) SecurityAudit.Record(db, "CompanyLogoRestored", "Company", "1", company.UpdatedByUserId);
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateConcurrencyException) { return Conflict(); }
        // Keep old logo files: issued-document snapshots may reference them in CONFIG-02D.
        return Results.Ok(new { version=Convert.ToBase64String(company.RowVersion) });
    }

    private static bool TryVersion(string version,out byte[] bytes)
    {
        bytes=[];
        try { bytes=Convert.FromBase64String(version); return bytes.Length==8; }
        catch(FormatException) { return false; }
    }
    private static IResult Conflict()=>Results.Conflict(new { message="La configuración cambió; recargá la página." });
}
