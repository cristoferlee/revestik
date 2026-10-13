using System.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>Records a narrowly scoped date-only discrepancy authorization.
/// Does not create a reference link, approve an adjustment or modify financial records.</summary>
public sealed class FiscalReferenceResolutionService(RevestikDbContext db)
{
    private const string DateAuthorization = "DateExceptionAuthorized";

    public async Task<IReadOnlyList<FiscalReferenceResolutionResponse>> GetHistoryAsync(
        int referenceId, CancellationToken ct)
    {
        if (!await db.ElectronicDocumentReferences.AsNoTracking()
                .AnyAsync(x => x.Id == referenceId, ct))
            throw new KeyNotFoundException("La referencia fiscal no existe.");
        var history = await db.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .Where(x => x.ElectronicDocumentReferenceId == referenceId)
            .OrderByDescending(x => x.ResolvedAtUtc).ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return history.Select(ToResponse).ToArray();
    }

    public async Task<FiscalReferenceResolutionResponse> AuthorizeDateExceptionAsync(
        int referenceId, FiscalDateExceptionRequest request, string userId, CancellationToken ct)
    {
        if (request.OriginalDocumentId <= 0)
            throw new ArgumentException("Debe seleccionar una factura original.");
        var justification = request.Justification?.Trim() ?? string.Empty;
        if (justification.Length < 20 || justification.Length > 2000)
            throw new ArgumentException("La justificación debe tener entre 20 y 2000 caracteres.");
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("No se encontró el usuario autenticado.");

        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, ct))
            throw new UnauthorizedAccessException("El usuario autorizador no existe o está inactivo.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reference = await db.ElectronicDocumentReferences
            .Include(x => x.ElectronicDocument)
            .SingleOrDefaultAsync(x => x.Id == referenceId, ct)
            ?? throw new KeyNotFoundException("La referencia fiscal no existe.");
        var note = reference.ElectronicDocument;
        var original = await db.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.OriginalDocumentId, ct)
            ?? throw new KeyNotFoundException("La factura original no existe.");

        if (note.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote) ||
            note.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.PendingReview)
            throw new InvalidOperationException("Solo se admiten excepciones para NC/ND pendientes de revisión.");
        if (note.Direction != ElectronicDocumentDirection.Received ||
            original.Direction != ElectronicDocumentDirection.Received ||
            note.OriginalXml.Length == 0 || original.OriginalXml.Length == 0)
            throw new InvalidOperationException("La resolución requiere documentos recibidos con XML original importado.");
        if (reference.RelatedElectronicDocumentId.HasValue)
            throw new InvalidOperationException("La referencia ya tiene un vínculo fiscal.");
        if (!ElectronicDocumentReferenceCompatibilityEvaluator.IsFiscalKey(reference.ReferenceNumber.Trim()) ||
            !string.Equals(reference.ReferenceNumber.Trim(), original.Clave.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Se requiere coincidencia exacta de clave fiscal de 50 dígitos.");

        var findings = ElectronicDocumentReferenceCompatibilityEvaluator.EvaluateDetailed(note, reference, original);
        if (findings.Count != 1 ||
            findings[0].Code != ElectronicDocumentReferenceCompatibilityEvaluator.CompatibilityIssue.IssueDate)
            throw new InvalidOperationException("Solo se permite autorizar una discrepancia de fecha; otras incompatibilidades permanecen bloqueadas: " + string.Join("; ", findings.Select(x => x.Message)));
        var reasons = findings.Select(x => x.Message).ToArray();
        if (await db.ElectronicDocumentReferenceResolutions.AnyAsync(x =>
                x.ElectronicDocumentReferenceId == referenceId && x.ResolutionType == DateAuthorization, ct))
            throw new InvalidOperationException("La referencia ya tiene una autorización de excepción.");

        var decision = new ElectronicDocumentReferenceResolution
        {
            ElectronicDocumentReferenceId = referenceId,
            OriginalDocumentId = original.Id,
            ResolutionType = DateAuthorization,
            Justification = justification,
            CompatibilityReasons = string.Join("; ", reasons),
            ResolvedByUserId = userId,
            ResolvedAtUtc = DateTime.UtcNow
        };
        db.ElectronicDocumentReferenceResolutions.Add(decision);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(decision);
    }

    private static FiscalReferenceResolutionResponse ToResponse(ElectronicDocumentReferenceResolution x) =>
        new(x.Id, x.ElectronicDocumentReferenceId, x.OriginalDocumentId, x.ResolutionType,
            x.Justification, x.CompatibilityReasons, x.ResolvedByUserId, x.ResolvedAtUtc);
}
