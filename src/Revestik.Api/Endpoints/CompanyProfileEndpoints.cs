using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Services.Audit;
using Revestik.Api.Integrations.Locations;
using Revestik.Api.Models;
using Revestik.Shared.Company;

namespace Revestik.Api.Endpoints;

public static class CompanyProfileEndpoints
{
    public static IEndpointRouteBuilder MapCompanyProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings/company")
            .WithTags("Company settings")
            .RequireAuthorization(PolicyNames.AdministratorOnly);

        group.MapGet("/", GetAsync).WithName("GetCompanyProfile");
        group.MapPut("/", UpdateAsync)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateCompanyProfile");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(RevestikDbContext db,
        IOptions<CompanyOptions> initialOptions, CancellationToken ct)
    {
        var company = await GetOrInitializeAsync(db, initialOptions.Value, ct);
        return Results.Ok(ToResponse(company));
    }

    private static async Task<IResult> UpdateAsync(UpdateCompanyProfileRequest request,
        RevestikDbContext db, IOptions<CompanyOptions> initialOptions,
        ILocationCatalogClient locations, ClaimsPrincipal principal, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        // Reject update if catalog availability cannot establish a valid location.
        var provinces = await locations.GetProvincesAsync(ct);
        if (provinces.Status != LocationCatalogStatus.Available) return Results.Problem(
            title: "No se pudo validar la ubicación.", statusCode: 503);
        if (!provinces.Locations.Any(x => x.Code == request.ProvinceCode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.ProvinceCode)] = ["Provincia inválida."] });
        var cantons = await locations.GetCantonsAsync(request.ProvinceCode, ct);
        if (cantons.Status != LocationCatalogStatus.Available) return LocationError(cantons.Status);
        if (!cantons.Locations.Any(x => x.Code == request.CantonCode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.CantonCode)] = ["Cantón inválido."] });
        var districts = await locations.GetDistrictsAsync(request.ProvinceCode, request.CantonCode, ct);
        if (districts.Status != LocationCatalogStatus.Available) return LocationError(districts.Status);
        if (!districts.Locations.Any(x => x.Code == request.DistrictCode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.DistrictCode)] = ["Distrito inválido."] });

        var company = await GetOrInitializeAsync(db, initialOptions.Value, ct);
        var version = request.Version ?? string.Empty;
        if (!Convert.TryFromBase64String(version,
            new Span<byte>(new byte[8]), out var written) || written != 8)
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Version)] = ["Versión inválida."] });
        var expected = Convert.FromBase64String(version);
        if (!company.RowVersion.SequenceEqual(expected))
            return Results.Conflict(new { message = "La empresa fue modificada por otro usuario. Volvé a cargar los datos." });

        var previous = new { company.CommercialName, company.PhoneNumber, company.Email, company.Website, company.ProvinceCode, company.CantonCode, company.DistrictCode, company.OtherSigns };
        company.CommercialName = request.CommercialName.Trim();
        company.PhoneNumber = request.PhoneNumber.Trim();
        company.Email = request.Email.Trim();
        company.Website = request.Website.Trim();
        company.ProvinceCode = request.ProvinceCode;
        company.CantonCode = request.CantonCode;
        company.DistrictCode = request.DistrictCode;
        company.OtherSigns = request.OtherSigns.Trim();
        company.Address = string.Join(", ", new[] {
            districts.Locations.Single(x => x.Code == request.DistrictCode).Name,
            cantons.Locations.Single(x => x.Code == request.CantonCode).Name,
            provinces.Locations.Single(x => x.Code == request.ProvinceCode).Name,
            company.OtherSigns }.Where(x => !string.IsNullOrWhiteSpace(x)));
        company.UpdatedByUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        company.UpdatedAtUtc = DateTime.UtcNow;
        var current = new { company.CommercialName, company.PhoneNumber, company.Email, company.Website, company.ProvinceCode, company.CantonCode, company.DistrictCode, company.OtherSigns };
        if (!Equals(previous, current))
            SecurityAudit.Record(db, "CompanyProfileUpdated", "Company", "1", company.UpdatedByUserId,
                new { previous, current });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { message = "La empresa fue modificada por otro usuario. Volvé a cargar los datos." });
        }
        return Results.Ok(ToResponse(company));
    }

    private static IResult LocationError(LocationCatalogStatus status) => status == LocationCatalogStatus.Unavailable
        ? Results.Problem(title: "Catálogo de ubicaciones no disponible.", statusCode: 503)
        : Results.ValidationProblem(new Dictionary<string, string[]> { ["Location"] = ["Ubicación inválida."] });

    private static Dictionary<string, string[]> Validate(UpdateCompanyProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        static string Safe(string? s) => s?.Trim() ?? string.Empty;
        void Check(string name, string? value, int max, bool required)
        {
            if (required && Safe(value).Length == 0) errors[name] = ["Campo obligatorio."];
            else if (Safe(value).Length > max) errors[name] = [$"Máximo {max} caracteres."];
        }
        Check(nameof(request.CommercialName), request.CommercialName, 150, true);
        Check(nameof(request.PhoneNumber), request.PhoneNumber, 20, true);
        Check(nameof(request.Email), request.Email, 254, true);
        Check(nameof(request.Website), request.Website, 255, false);
        Check(nameof(request.OtherSigns), request.OtherSigns, 160, true);
        Check(nameof(request.ProvinceCode), request.ProvinceCode, 1, true);
        Check(nameof(request.CantonCode), request.CantonCode, 2, true);
        Check(nameof(request.DistrictCode), request.DistrictCode, 2, true);
        if (!new EmailAddressAttribute().IsValid(request.Email)) errors[nameof(request.Email)] = ["Correo inválido."];
        if (request.ProvinceCode?.Length != 1 || !request.ProvinceCode.All(char.IsDigit) ||
            request.CantonCode?.Length != 2 || !request.CantonCode.All(char.IsDigit) ||
            request.DistrictCode?.Length != 2 || !request.DistrictCode.All(char.IsDigit))
            errors["Location"] = ["Códigos de ubicación inválidos."];
        return errors;
    }

    private static async Task<CompanyProfile> GetOrInitializeAsync(RevestikDbContext db,
        CompanyOptions options, CancellationToken ct)
    {
        var company = await db.Set<CompanyProfile>().SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (company is not null) return company;
        company = new CompanyProfile {
            Id = 1, LegalName = options.LegalName,
            IdentificationNumber = options.IdentificationNumber,
            TaxIdentificationType = options.TaxIdentificationType,
            TaxIdentificationNumber = options.TaxIdentificationNumber,
            PhoneNumber = options.PhoneNumber, Email = options.Email,
            Address = options.Address, Website = options.Website,
            CommercialName = "Revestik", UpdatedAtUtc = DateTime.UtcNow
        };
        db.Set<CompanyProfile>().Add(company);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(company).State = EntityState.Detached;
            company = await db.Set<CompanyProfile>().SingleAsync(x => x.Id == 1, ct);
        }
        return company;
    }

    private static CompanyProfileResponse ToResponse(CompanyProfile company) => new(
        company.LegalName, company.IdentificationNumber,
        company.TaxIdentificationType, company.TaxIdentificationNumber,
        company.CommercialName, company.PhoneNumber, company.Email, company.Website,
        company.ProvinceCode, company.CantonCode, company.DistrictCode,
        company.OtherSigns, company.Address, Convert.ToBase64String(company.RowVersion));
}
