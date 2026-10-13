using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Recognizes a previously audited date-only exception for precisely one reference
/// and original document. Never creates decisions, links documents, or approves notes.
/// </summary>
internal static class FiscalReferenceDateExceptionValidator
{
    private const string DateAuthorization = "DateExceptionAuthorized";

    public static async Task<bool> IsAuthorizedAsync(
        RevestikDbContext db,
        ElectronicDocument note,
        ElectronicDocumentReference reference,
        ElectronicDocument original,
        CancellationToken ct)
    {
        // An exceptional link must never weaken the import/admission requirements
        // established by the resolution authorization service.
        if (note.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote) ||
            note.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.PendingReview ||
            note.Direction != ElectronicDocumentDirection.Received ||
            original.Direction != ElectronicDocumentDirection.Received ||
            note.OriginalXml is not { Length: > 0 } ||
            original.OriginalXml is not { Length: > 0 })
            return false;

        var number = reference.ReferenceNumber?.Trim();
        if (!ElectronicDocumentReferenceCompatibilityEvaluator.IsFiscalKey(number) ||
            !string.Equals(number, original.Clave?.Trim(), StringComparison.Ordinal))
            return false;

        var findings = ElectronicDocumentReferenceCompatibilityEvaluator.EvaluateDetailed(note, reference, original);
        if (findings.Count != 1 ||
            findings[0].Code != ElectronicDocumentReferenceCompatibilityEvaluator.CompatibilityIssue.IssueDate)
            return false;

        return await db.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .AnyAsync(x => x.ElectronicDocumentReferenceId == reference.Id &&
                           x.OriginalDocumentId == original.Id &&
                           x.ResolutionType == DateAuthorization, ct);
    }
}
