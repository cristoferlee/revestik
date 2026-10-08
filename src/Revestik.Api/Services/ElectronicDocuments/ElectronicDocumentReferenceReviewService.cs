using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;
using Microsoft.EntityFrameworkCore;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Coordinates review of imported note references. Suggestions are never persisted
/// automatically; linking requires an explicit action in the calling workflow.
/// </summary>
public sealed class ElectronicDocumentReferenceReviewService(
    RevestikDbContext dbContext,
    ElectronicDocumentReferenceLinkService linkService)
{
    public async Task<IReadOnlyList<ElectronicDocumentReferenceSuggestion>> ReviewAsync(
        int noteId, CancellationToken cancellationToken = default)
    {
        var note = await dbContext.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == noteId, cancellationToken)
            ?? throw new KeyNotFoundException("El comprobante no existe.");
        if (note.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote))
            throw new InvalidOperationException("Solo se pueden revisar notas de crédito y débito.");
        if (note.AdjustmentStatus is ElectronicDocumentAdjustmentStatus.Approved
            or ElectronicDocumentAdjustmentStatus.Applied)
            throw new InvalidOperationException("El ajuste ya no está pendiente de revisión.");
        return await linkService.SuggestAsync(noteId, cancellationToken);
    }

    public async Task LinkAfterReviewAsync(
        int referenceId, int originalDocumentId,
        CancellationToken cancellationToken = default)
    {
        // LinkAsync independently checks identity, fiscal type, issue date and review status.
        await linkService.LinkAsync(referenceId, originalDocumentId, cancellationToken);
    }
}
