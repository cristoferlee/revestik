using System.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

// These records track project operations; they never create a SalePayment or fiscal document.
public sealed class ProjectPaymentService(RevestikDbContext db)
{
    public async Task<ProjectBalanceResponse?> SummaryAsync(int projectId, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return null;
        var paid = await ActiveTotalAsync(projectId, ct);
        return new(projectId, project.AgreedAmount, project.CurrencyCode, paid,
            project.AgreedAmount is null ? null : project.AgreedAmount - paid);
    }

    public async Task<IReadOnlyList<ProjectPaymentResponse>?> ListAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(x => x.Id == projectId, ct)) return null;
        return await db.Set<ProjectPayment>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.Id)
            .Select(x => new ProjectPaymentResponse(x.Id, x.ProjectId, x.Amount, x.CurrencyCode,
                x.PaidOn, x.Reference, x.Notes, x.Status, x.Version, x.CreatedByUserId,
                x.CreatedAtUtc, x.VoidedAtUtc, x.VoidReason, x.DocumentLinks.Any()))
            .ToListAsync(ct);
    }

    public async Task<ProjectPaymentResponse?> GetAsync(int projectId, int paymentId, CancellationToken ct)
    {
        var x = await db.Set<ProjectPayment>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == paymentId, ct);
        if (x is null) return null;
        var evidence = await db.Set<ProjectPaymentDocumentLink>().AnyAsync(y => y.ProjectPaymentId == paymentId, ct);
        return Map(x, evidence);
    }

    public async Task<IReadOnlyList<ProjectPaymentRevisionResponse>?> HistoryAsync(int projectId, int paymentId, CancellationToken ct)
    {
        if (!await db.Set<ProjectPayment>().AnyAsync(x => x.Id == paymentId && x.ProjectId == projectId, ct)) return null;
        return await db.Set<ProjectPaymentRevision>().AsNoTracking()
            .Where(x => x.ProjectPaymentId == paymentId).OrderByDescending(x => x.Id)
            .Select(x => new ProjectPaymentRevisionResponse(x.Id, x.PreviousVersion, x.PreviousAmount,
                x.PreviousCurrencyCode, x.PreviousPaidOn, x.PreviousReference, x.PreviousNotes,
                x.Reason, x.ChangedByUserId, x.ChangedAtUtc)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectAgreedAmountRevisionResponse>?> AmountHistoryAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(x => x.Id == projectId, ct)) return null;
        return await db.Set<ProjectAgreedAmountRevision>().AsNoTracking()
            .Where(x => x.ProjectId == projectId).OrderByDescending(x => x.Id)
            .Select(x => new ProjectAgreedAmountRevisionResponse(x.Id, x.PreviousProjectVersion,
                x.PreviousAmount, x.NewAmount, x.PreviousCurrencyCode, x.NewCurrencyCode,
                x.Reason, x.ChangedByUserId, x.ChangedAtUtc)).ToListAsync(ct);
    }

    public async Task<ProjectPaymentResponse> CreateAsync(int projectId, ProjectPaymentUpsertRequest request,
        string actor, bool admin, CancellationToken ct)
    {
        ValidatePayment(request);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var project = await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        ValidateCurrency(project, request.CurrencyCode);
        var total = await ActiveTotalAsync(projectId, ct);
        EnsureWithinBalance(project.AgreedAmount!.Value, total + request.Amount);
        var payment = new ProjectPayment { ProjectId = projectId, Amount = request.Amount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(), PaidOn = request.PaidOn,
            Reference = request.Reference?.Trim() ?? "", Notes = request.Notes?.Trim() ?? "",
            CreatedByUserId = actor, CreatedAtUtc = DateTime.UtcNow };
        db.Set<ProjectPayment>().Add(payment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(payment, false);
    }

    public async Task<ProjectPaymentResponse?> CorrectAsync(int projectId, int paymentId,
        ProjectPaymentUpsertRequest request, string actor, bool admin, CancellationToken ct)
    {
        ValidatePayment(request);
        RequireReason(request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var project = await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        var payment = await db.Set<ProjectPayment>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == paymentId, ct);
        if (payment is null) return null;
        if (payment.Status != "Active") throw Invalid("No se puede corregir un abono anulado.");
        if (request.Version != payment.Version) throw Conflict();
        ValidateCurrency(project, request.CurrencyCode);
        var total = await ActiveTotalAsync(projectId, ct);
        EnsureWithinBalance(project.AgreedAmount!.Value, total - payment.Amount + request.Amount);
        db.Set<ProjectPaymentRevision>().Add(new ProjectPaymentRevision {
            ProjectPaymentId = payment.Id, PreviousVersion = payment.Version, PreviousAmount = payment.Amount,
            PreviousCurrencyCode = payment.CurrencyCode, PreviousPaidOn = payment.PaidOn,
            PreviousReference = payment.Reference, PreviousNotes = payment.Notes,
            ChangedByUserId = actor, ChangedAtUtc = DateTime.UtcNow, Reason = request.Reason!.Trim() });
        payment.Amount = request.Amount;
        payment.PaidOn = request.PaidOn;
        payment.Reference = request.Reference?.Trim() ?? "";
        payment.Notes = request.Notes?.Trim() ?? "";
        payment.Version++;
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Map(payment, await db.Set<ProjectPaymentDocumentLink>().AnyAsync(x => x.ProjectPaymentId == paymentId, ct));
    }

    public async Task<bool> VoidAsync(int projectId, int paymentId, ProjectPaymentVoidRequest request,
        string actor, bool admin, CancellationToken ct)
    {
        RequireReason(request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        var payment = await db.Set<ProjectPayment>().SingleOrDefaultAsync(x => x.Id == paymentId && x.ProjectId == projectId, ct);
        if (payment is null) return false;
        if (payment.Status != "Active" || payment.Version != request.Version) throw Conflict();
        payment.Status = "Voided";
        payment.VoidReason = request.Reason.Trim();
        payment.VoidedByUserId = actor;
        payment.VoidedAtUtc = DateTime.UtcNow;
        payment.Version++;
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<ProjectBalanceResponse> ChangeAgreedAmountAsync(int projectId,
        ProjectAgreedAmountChangeRequest request, string actor, bool admin, CancellationToken ct)
    {
        RequireReason(request.Reason);
        if (request.Amount is < 0 || request.Amount is > 9999999999999999.99m ||
            (request.Amount.HasValue && decimal.Round(request.Amount.Value, 2) != request.Amount.Value) ||
            (request.Amount is null) != (request.CurrencyCode is null) ||
            (request.CurrencyCode is not null && request.CurrencyCode is not ("CRC" or "USD")))
            throw Invalid("El monto acordado o la moneda no son válidos.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var project = await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        if (project.Version != request.Version) throw Conflict();
        var count = await db.Set<ProjectPayment>().CountAsync(x => x.ProjectId == projectId, ct);
        var paid = await ActiveTotalAsync(projectId, ct);
        if (count > 0 && (request.Amount is null || request.CurrencyCode != project.CurrencyCode))
            throw Invalid("No puede retirar el monto ni cambiar la moneda cuando existe historial de abonos.");
        if (request.Amount is not null) EnsureWithinBalance(request.Amount.Value, paid);
        if (project.AgreedAmount == request.Amount && project.CurrencyCode == request.CurrencyCode)
            throw Invalid("El monto acordado no presenta cambios.");
        db.Set<ProjectAgreedAmountRevision>().Add(new ProjectAgreedAmountRevision {
            ProjectId = projectId, PreviousProjectVersion = project.Version,
            PreviousAmount = project.AgreedAmount, NewAmount = request.Amount,
            PreviousCurrencyCode = project.CurrencyCode, NewCurrencyCode = request.CurrencyCode,
            ChangedByUserId = actor, ChangedAtUtc = DateTime.UtcNow, Reason = request.Reason.Trim() });
        project.AgreedAmount = request.Amount;
        project.CurrencyCode = request.CurrencyCode;
        project.Version++;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return new(projectId, project.AgreedAmount, project.CurrencyCode, paid,
            project.AgreedAmount is null ? null : project.AgreedAmount - paid);
    }

    public async Task<bool> LinkDocumentAsync(int projectId, int paymentId, int documentId,
        string actor, bool admin, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        var exists = await db.Set<ProjectPayment>().AnyAsync(x => x.Id == paymentId && x.ProjectId == projectId, ct);
        if (!exists) return false;
        if (!await db.Set<ProjectDocument>().AnyAsync(x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct))
            throw Invalid("El comprobante no existe o pertenece a otro proyecto.");
        if (!await db.Set<ProjectPaymentDocumentLink>().AnyAsync(x => x.ProjectPaymentId == paymentId && x.ProjectDocumentId == documentId, ct))
        {
            db.Set<ProjectPaymentDocumentLink>().Add(new ProjectPaymentDocumentLink {
                ProjectId = projectId, ProjectPaymentId = paymentId, ProjectDocumentId = documentId,
                LinkedByUserId = actor, LinkedAtUtc = DateTime.UtcNow });
            await SaveAsync(ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> UnlinkDocumentAsync(int projectId, int paymentId, int documentId,
        string actor, bool admin, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ProjectPaymentWriteLock.LoadAsync(db, projectId, actor, admin, ct);
        if (!await db.Set<ProjectPayment>().AnyAsync(x => x.Id == paymentId && x.ProjectId == projectId, ct)) return false;
        var link = await db.Set<ProjectPaymentDocumentLink>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.ProjectPaymentId == paymentId && x.ProjectDocumentId == documentId, ct);
        if (link is not null) { db.Remove(link); await SaveAsync(ct); }
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<decimal> ActiveTotalAsync(int projectId, CancellationToken ct) =>
        await db.Set<ProjectPayment>().Where(x => x.ProjectId == projectId && x.Status == "Active")
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

    private static void ValidatePayment(ProjectPaymentUpsertRequest request)
    {
        if (request.Amount <= 0 || request.Amount > 9999999999999999.99m ||
            decimal.Round(request.Amount, 2) != request.Amount || request.PaidOn == default ||
            request.Reference?.Length > 200 || request.Notes?.Length > 1000 ||
            request.CurrencyCode is null || request.CurrencyCode.Trim().ToUpperInvariant() is not ("CRC" or "USD"))
            throw Invalid("Los datos del abono no son válidos.");
    }
    private static void ValidateCurrency(Project project, string currency)
    {
        if (project.AgreedAmount is null || project.CurrencyCode != currency.Trim().ToUpperInvariant())
            throw Invalid("Configure el monto acordado y registre el abono en la moneda del proyecto.");
    }
    private static void EnsureWithinBalance(decimal budget, decimal paid)
    {
        if (paid > budget) throw new ProjectOperationException(ProjectFailureReason.Invalid, "El abono supera el saldo disponible.");
    }
    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw Invalid("El motivo es obligatorio y debe tener hasta 500 caracteres.");
    }
    private static ProjectOperationException Invalid(string message) => new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() => new(ProjectFailureReason.ConcurrencyConflict, "Los datos cambiaron. Actualice antes de continuar.");
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
    }
    private static ProjectPaymentResponse Map(ProjectPayment x, bool evidence) =>
        new(x.Id, x.ProjectId, x.Amount, x.CurrencyCode, x.PaidOn, x.Reference, x.Notes,
            x.Status, x.Version, x.CreatedByUserId, x.CreatedAtUtc, x.VoidedAtUtc, x.VoidReason, evidence);
}
