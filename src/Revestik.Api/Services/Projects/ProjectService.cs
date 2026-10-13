using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public sealed class ProjectService(RevestikDbContext db) : IProjectService
{
    public async Task<IReadOnlyList<ProjectSummaryResponse>> ListAsync(int customerId, CancellationToken ct) =>
        await db.Set<Project>().AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ProjectSummaryResponse(x.Id, x.CustomerId, x.Name, x.Status,
                x.ResponsibleUserId, x.CreatedAtUtc)).ToListAsync(ct);

    public async Task<ProjectResponse?> GetAsync(int id, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return project is null ? null : Map(project);
    }

    public async Task<ProjectResponse> CreateAsync(int customerId, ProjectUpsertRequest request,
        string creatorUserId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, ct);
        if (customer is null || !customer.IsActive)
            throw new ProjectOperationException(ProjectFailureReason.InactiveCustomer,
                "Seleccione un cliente activo para crear un proyecto.");
        if (!await db.Users.AnyAsync(x => x.Id == creatorUserId, ct))
            throw new ProjectOperationException(ProjectFailureReason.UserNotFound, "Usuario no encontrado.");

        var clean = Validate(request);
        var project = new Project { CustomerId = customerId, ResponsibleUserId = creatorUserId,
            Status = ProjectStatus.Planning, CreatedAtUtc = DateTime.UtcNow };
        Assign(project, clean);
        db.Set<Project>().Add(project);
        await SaveAsync(ct);
        return Map(project);
    }

    public async Task<ProjectResponse?> UpdateAsync(int id, ProjectUpsertRequest request,
        string actorUserId, bool isAdmin, CancellationToken ct)
    {
        var project = await db.Set<Project>().Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null) return null;
        EnsureEditor(project, actorUserId, isAdmin);
        RequireCurrentVersion(project, request.Version);
        var clean = Validate(request);
        Assign(project, clean);
        project.Version++;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
        return Map(project);
    }

    public async Task<ProjectResponse?> ReassignAsync(int id, string newResponsibleUserId, long version, CancellationToken ct)
    {
        var project = await db.Set<Project>().Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null) return null;
        if (string.IsNullOrWhiteSpace(newResponsibleUserId) ||
            !await db.Users.AnyAsync(x => x.Id == newResponsibleUserId, ct))
            throw new ProjectOperationException(ProjectFailureReason.UserNotFound,
                "El responsable seleccionado no existe.");
        RequireCurrentVersion(project, version);
        project.ResponsibleUserId = newResponsibleUserId;
        project.Version++;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
        return Map(project);
    }

    public async Task<bool> DeleteAsync(int id, string actorUserId, bool isAdmin, long version, CancellationToken ct)
    {
        var project = await db.Set<Project>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null) return false;
        EnsureEditor(project, actorUserId, isAdmin);
        RequireCurrentVersion(project, version);
        db.Set<Project>().Remove(project);
        await SaveAsync(ct);
        return true;
    }

    private static void RequireCurrentVersion(Project project, long? expected)
    {
        if (expected is null or <= 0)
            throw Invalid("Debe proporcionar una versión válida del proyecto.");
        if (project.Version != expected.Value)
            throw new ProjectOperationException(ProjectFailureReason.ConcurrencyConflict,
                "El proyecto cambió desde su última consulta. Actualice la información antes de continuar.");
    }

    private static void EnsureEditor(Project project, string actorId, bool admin)
    {
        if (!admin && project.ResponsibleUserId != actorId)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable del proyecto o un administrador puede modificarlo.");
    }

    private static ProjectUpsertRequest Validate(ProjectUpsertRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
            throw Invalid("El nombre debe tener entre 1 y 150 caracteres.");
        if ((request.Description?.Length ?? 0) > 2000 || (request.OtherSigns?.Length ?? 0) > 160)
            throw Invalid("La descripción o las señas exceden el máximo permitido.");
        if (request.EstimatedStartDate > request.EstimatedEndDate)
            throw Invalid("La fecha final no puede ser anterior a la inicial.");
        if (request.AgreedAmount is < 0 || request.AgreedAmount is > 9999999999999999.99m)
            throw Invalid("El importe acordado no es válido.");
        var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
        if ((request.AgreedAmount is null) != (string.IsNullOrEmpty(currency)))
            throw Invalid("El importe y la moneda deben especificarse juntos.");
        if (currency is not null && currency is not ("CRC" or "USD"))
            throw Invalid("La moneda debe ser CRC o USD.");
        if (!ValidLocation(request.ProvinceCode, 1) || !ValidLocation(request.CantonCode, 2)
            || !ValidLocation(request.DistrictCode, 2))
            throw Invalid("La ubicación contiene códigos inválidos.");
        if ((request.ProvinceCode is null) != (request.CantonCode is null) ||
            (request.ProvinceCode is null) != (request.DistrictCode is null))
            throw Invalid("Provincia, cantón y distrito deben indicarse juntos.");
        if (request.Contacts is { Count: > 30 })
            throw Invalid("El proyecto supera el máximo de contactos.");
        foreach (var contact in request.Contacts ?? [])
        {
            if (string.IsNullOrWhiteSpace(contact.Name) || contact.Name.Trim().Length > 150 ||
                string.IsNullOrWhiteSpace(contact.Role) || contact.Role.Trim().Length > 100 ||
                contact.PhoneNumber?.Length > 20 || contact.Email?.Length > 254)
                throw Invalid("Los datos de contacto son inválidos.");
            if (!string.IsNullOrWhiteSpace(contact.Email) &&
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(contact.Email))
                throw Invalid("El correo del contacto no es válido.");
        }
        return request with { CurrencyCode = currency };
    }

    private static bool ValidLocation(string? value, int digits) =>
        value is null || (value.Length == digits && value.All(char.IsDigit));

    private static ProjectOperationException Invalid(string message) =>
        new(ProjectFailureReason.Invalid, message);

    private static void Assign(Project project, ProjectUpsertRequest request)
    {
        project.Name = request.Name.Trim();
        project.NormalizedName = project.Name.ToUpperInvariant();
        project.Description = request.Description?.Trim() ?? string.Empty;
        project.ProvinceCode = request.ProvinceCode;
        project.CantonCode = request.CantonCode;
        project.DistrictCode = request.DistrictCode;
        project.OtherSigns = request.OtherSigns?.Trim();
        project.EstimatedStartDate = request.EstimatedStartDate;
        project.EstimatedEndDate = request.EstimatedEndDate;
        project.AgreedAmount = request.AgreedAmount;
        project.CurrencyCode = request.CurrencyCode;
        project.Contacts.Clear();
        foreach (var contact in request.Contacts ?? [])
            project.Contacts.Add(new ProjectContact { Name = contact.Name.Trim(), Role = contact.Role.Trim(),
                PhoneNumber = contact.PhoneNumber?.Trim(), Email = contact.Email?.Trim() });
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProjectOperationException(ProjectFailureReason.ConcurrencyConflict,
                "El proyecto fue modificado por otro usuario. Actualice la información antes de continuar.");
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627 &&
            sqlException.Message.Contains("UX_Projects_Customer_NormalizedName", StringComparison.Ordinal))
        {
            throw new ProjectOperationException(ProjectFailureReason.Duplicate,
                "Ya existe un proyecto con este nombre para el cliente seleccionado.");
        }
    }

    private static ProjectResponse Map(Project x) => new(x.Id, x.CustomerId, x.Name,
        x.Description, x.ResponsibleUserId, x.Status, x.ProvinceCode, x.CantonCode,
        x.DistrictCode, x.OtherSigns, x.EstimatedStartDate, x.EstimatedEndDate,
        x.AgreedAmount, x.CurrencyCode, x.CreatedAtUtc, x.UpdatedAtUtc,
        x.Contacts.OrderBy(c => c.Id).Select(c =>
            new ProjectContactResponse(c.Id, c.Name, c.Role, c.PhoneNumber, c.Email)).ToList(), x.Version);
}
