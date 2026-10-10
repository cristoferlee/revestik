using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>Approves documentary adjustments only. Never changes purchases, balances or stock.</summary>
public sealed class FiscalApprovalService(RevestikDbContext db)
{
    public async Task ApproveAsync(int noteId, FiscalApprovalRequest request, CancellationToken ct)
    {
        if (request.ReferenceId <= 0 || request.OriginalDocumentId <= 0)
            throw new InvalidOperationException("Debe seleccionar una referencia y una factura original.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var note = await db.ElectronicDocuments.Include(x => x.References)
            .SingleOrDefaultAsync(x => x.Id == noteId, ct)
            ?? throw new KeyNotFoundException("La nota fiscal no existe.");

        if (note.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote))
            throw new InvalidOperationException("Solo se pueden aprobar NC o ND.");
        if (note.AdjustmentStatus is ElectronicDocumentAdjustmentStatus.Approved or ElectronicDocumentAdjustmentStatus.Applied)
            throw new InvalidOperationException("La nota ya fue aprobada; actualice la bandeja.");
        if (note.References.Count != 1 || note.References.Single().Id != request.ReferenceId)
            throw new InvalidOperationException("Las notas con múltiples referencias requieren asignación individual de importes.");

        var reference = note.References.Single();
        if (reference.RelatedElectronicDocumentId.HasValue &&
            reference.RelatedElectronicDocumentId.Value != request.OriginalDocumentId)
            throw new InvalidOperationException("La referencia ya apunta a otro comprobante.");

        // This existing service independently validates identity, fiscal type, issue date and key.
        await new ElectronicDocumentReferenceLinkService(db).LinkAsync(
            request.ReferenceId, request.OriginalDocumentId, ct);

        var original = await db.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.OriginalDocumentId, ct)
            ?? throw new KeyNotFoundException("La factura original no existe.");
        if (!string.Equals(original.CurrencyCode, note.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La moneda de la nota y la factura no coincide.");

        // Conditional database update prevents concurrent approvals from counting twice.
        var changed = await db.ElectronicDocuments
            .Where(x => x.Id == noteId && x.AdjustmentStatus == ElectronicDocumentAdjustmentStatus.PendingReview)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AdjustmentStatus, ElectronicDocumentAdjustmentStatus.Approved), ct);
        if (changed != 1)
            throw new InvalidOperationException("La nota cambió de estado durante la revisión.");

        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<FiscalAdjustedTotal>> GetAdjustedTotalsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0) return [];
        var ids = documentIds.Distinct().Take(100).ToArray();
        var originals = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.CurrencyCode, x.TotalDocument })
            .ToListAsync(ct);

        var approved = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => x.AdjustmentStatus == ElectronicDocumentAdjustmentStatus.Approved ||
                        x.AdjustmentStatus == ElectronicDocumentAdjustmentStatus.Applied)
            .Where(x => x.DocumentType == ElectronicDocumentType.CreditNote ||
                        x.DocumentType == ElectronicDocumentType.DebitNote)
            .Where(x => x.References.Count() == 1 &&
                        x.References.Any(r => r.RelatedElectronicDocumentId.HasValue &&
                            ids.Contains(r.RelatedElectronicDocumentId.Value)))
            .Select(x => new
            {
                x.Id, x.Clave, x.DocumentType, x.CurrencyCode, x.TotalDocument,
                OriginalId = x.References.Select(r => r.RelatedElectronicDocumentId).Single()
            }).ToListAsync(ct);

        return originals.Select(original =>
        {
            var notes = approved.Where(x => x.OriginalId == original.Id &&
                string.Equals(x.CurrencyCode, original.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                .Select(x => new FiscalAdjustmentCalculator.Note(x.Id, x.Clave, x.DocumentType,
                    x.CurrencyCode, x.TotalDocument));
            return FiscalAdjustmentCalculator.Calculate(original.Id, original.CurrencyCode,
                original.TotalDocument, notes);
        }).ToArray();
    }
}
