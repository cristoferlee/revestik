using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Resolves fiscal references without applying accounting, purchase, payment or stock effects.
/// This service is deliberately not exposed as an endpoint until document acceptance is enabled.
/// </summary>
public sealed class ElectronicDocumentReferenceLinkService(RevestikDbContext dbContext)
{
    public async Task<IReadOnlyList<ElectronicDocumentReferenceSuggestion>> SuggestAsync(
        int adjustmentDocumentId,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.ElectronicDocuments.AsNoTracking()
            .Include(x => x.References)
            .SingleOrDefaultAsync(x => x.Id == adjustmentDocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("El comprobante no existe.");

        EnsureAdjustment(source);
        var results = new List<ElectronicDocumentReferenceSuggestion>();

        foreach (var reference in source.References.OrderBy(x => x.Sequence))
        {
            var number = reference.ReferenceNumber.Trim();
            // Only exact 50-digit Clave matches can be suggested automatically.
            // Consecutive numbers are not globally unique and require explicit review.
            if (!ElectronicDocumentReferenceCompatibilityEvaluator.IsFiscalKey(number))
            {
                results.Add(new(reference.Id, null, "RequiresManualReview"));
                continue;
            }

            var candidates = await dbContext.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Clave == number && x.Id != source.Id)
                .ToListAsync(cancellationToken);

            var compatible = candidates.Where(x => ElectronicDocumentReferenceCompatibilityEvaluator.Evaluate(source, reference, x).Count == 0).ToList();
            results.Add(compatible.Count == 1
                ? new(reference.Id, compatible[0].Id, "ExactMatch")
                : new(reference.Id, null, compatible.Count == 0 ? (candidates.Count == 0 ? "NotFound" : "Incompatible") : "Ambiguous"));
        }

        return results;
    }

    /// <summary>
    /// Explicitly links an existing fiscal reference to an imported document.
    /// This does not change AdjustmentStatus or any economic balance.
    /// </summary>
    public async Task LinkAsync(int referenceId, int relatedDocumentId,
        CancellationToken cancellationToken = default)
    {
        var reference = await dbContext.Set<ElectronicDocumentReference>()
            .Include(x => x.ElectronicDocument)
            .SingleOrDefaultAsync(x => x.Id == referenceId, cancellationToken)
            ?? throw new KeyNotFoundException("La referencia fiscal no existe.");

        var target = await dbContext.ElectronicDocuments
            .SingleOrDefaultAsync(x => x.Id == relatedDocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("El comprobante original no existe.");

        EnsureAdjustment(reference.ElectronicDocument);
        if (reference.ElectronicDocument.AdjustmentStatus is ElectronicDocumentAdjustmentStatus.Approved
            or ElectronicDocumentAdjustmentStatus.Applied)
            throw new InvalidOperationException("No se puede modificar la vinculación de un ajuste aprobado o aplicado.");

        var reasons = ElectronicDocumentReferenceCompatibilityEvaluator.Evaluate(
            reference.ElectronicDocument, reference, target);
        if (reasons.Count > 0)
            throw new InvalidOperationException(
                "La referencia no coincide fiscalmente: " + string.Join("; ", reasons));

        if (reference.RelatedElectronicDocumentId == relatedDocumentId)
            return;

        if (reference.RelatedElectronicDocumentId.HasValue)
            throw new InvalidOperationException("La referencia ya está vinculada. Se requiere un flujo explícito de corrección.");

        // EF Core InMemory is used by existing unit tests and does not support
        // ExecuteUpdate. Production uses the conditional database update below.
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            reference.RelatedElectronicDocumentId = relatedDocumentId;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // The reference must STILL be unlinked when SQL performs the write.
        // Include adjustment status in the predicate, so a concurrently approved
        // note cannot have its reference linked after approval.
        var changed = await dbContext.Set<ElectronicDocumentReference>()
            .Where(x => x.Id == referenceId &&
                        x.RelatedElectronicDocumentId == null &&
                        x.ElectronicDocument.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.Approved &&
                        x.ElectronicDocument.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.Applied)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RelatedElectronicDocumentId, relatedDocumentId),
                cancellationToken);

        if (changed != 1)
            throw new InvalidOperationException(
                "La referencia fue vinculada o la nota cambió de estado durante la revisión. Actualice la información.");

        // ExecuteUpdate bypasses EF tracking; refresh the tracked entity so
        // the caller sees the persisted relationship and cannot save stale data.
        await dbContext.Entry(reference).ReloadAsync(cancellationToken);
    }

    private static void EnsureAdjustment(ElectronicDocument document)
    {
        if (document.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote))
            throw new InvalidOperationException("Solo se pueden vincular referencias de notas de crédito o débito.");
    }


}

public sealed record ElectronicDocumentReferenceSuggestion(int ReferenceId, int? SuggestedDocumentId, string Result);
