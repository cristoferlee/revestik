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
            if (!IsFiscalKey(number))
            {
                results.Add(new(reference.Id, null, "RequiresManualReview"));
                continue;
            }

            var candidates = await dbContext.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Clave == number && x.Id != source.Id)
                .ToListAsync(cancellationToken);

            var compatible = candidates.Where(x => IsCompatible(source, reference, x)).ToList();
            results.Add(compatible.Count == 1
                ? new(reference.Id, compatible[0].Id, "ExactMatch")
                : new(reference.Id, null, compatible.Count == 0 ? "NotFoundOrIncompatible" : "Ambiguous"));
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

        if (!IsCompatible(reference.ElectronicDocument, reference, target))
            throw new InvalidOperationException("La referencia no coincide fiscalmente con el comprobante seleccionado.");

        if (reference.RelatedElectronicDocumentId == relatedDocumentId)
            return;

        if (reference.RelatedElectronicDocumentId.HasValue)
            throw new InvalidOperationException("La referencia ya está vinculada. Se requiere un flujo explícito de corrección.");

        reference.RelatedElectronicDocumentId = relatedDocumentId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureAdjustment(ElectronicDocument document)
    {
        if (document.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote))
            throw new InvalidOperationException("Solo se pueden vincular referencias de notas de crédito o débito.");
    }

    private static bool IsCompatible(ElectronicDocument source,
        ElectronicDocumentReference reference, ElectronicDocument target)
    {
        if (source.Id == target.Id || source.Direction != target.Direction)
            return false;

        if (string.IsNullOrWhiteSpace(source.IssuerIdentification) ||
            string.IsNullOrWhiteSpace(source.ReceiverIdentification) ||
            !string.Equals(source.IssuerIdentification, target.IssuerIdentification, StringComparison.Ordinal) ||
            !string.Equals(source.ReceiverIdentification, target.ReceiverIdentification, StringComparison.Ordinal) ||
            !string.Equals(source.IssuerIdentificationType, target.IssuerIdentificationType, StringComparison.Ordinal) ||
            !string.Equals(source.ReceiverIdentificationType, target.ReceiverIdentificationType, StringComparison.Ordinal))
            return false;

        var number = reference.ReferenceNumber.Trim();
        if (!string.Equals(number, target.Clave, StringComparison.Ordinal) &&
            !string.Equals(number, target.NumeroConsecutivo, StringComparison.Ordinal))
            return false;

        if (!TryMapFiscalTypeCode(reference.ReferencedDocumentTypeCode, out var expectedType) ||
            target.DocumentType != expectedType)
            return false;

        // A fiscal reference can include different timezone offsets; compare instants.
        return reference.ReferencedIssueDate.ToUniversalTime() == target.FechaEmision.ToUniversalTime();
    }

    private static bool IsFiscalKey(string number) =>
        number.Length == 50 && number.All(char.IsAsciiDigit);

    private static bool TryMapFiscalTypeCode(string code, out ElectronicDocumentType documentType)
    {
        documentType = code switch
        {
            "01" => ElectronicDocumentType.Invoice,
            "02" => ElectronicDocumentType.DebitNote,
            "03" => ElectronicDocumentType.CreditNote,
            "04" => ElectronicDocumentType.ElectronicTicket,
            "08" => ElectronicDocumentType.PurchaseInvoice,
            "09" => ElectronicDocumentType.ExportInvoice,
            "10" => ElectronicDocumentType.ElectronicPaymentReceipt,
            _ => default
        };
        return code is "01" or "02" or "03" or "04" or "08" or "09" or "10";
    }
}

public sealed record ElectronicDocumentReferenceSuggestion(int ReferenceId, int? SuggestedDocumentId, string Result);
