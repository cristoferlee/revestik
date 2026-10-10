using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;
namespace Revestik.Api.Services.ElectronicDocuments;
/// <summary>Read-only discovery; the existing link services enforce all write-time rules.</summary>
public sealed class FiscalReconciliationCandidateService(RevestikDbContext db)
{
    public async Task<FiscalWorklistResponse> ListAsync(string kind, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        // The primary worklist is actionable work, never a dump of all fiscal documents.
        var query = db.ElectronicDocuments.AsNoTracking();
        if (kind == "notes")
        {
            query = query.Where(x => (x.DocumentType == ElectronicDocumentType.CreditNote ||
                x.DocumentType == ElectronicDocumentType.DebitNote) &&
                x.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.Approved &&
                x.AdjustmentStatus != ElectronicDocumentAdjustmentStatus.Applied);
        }
        else if (kind == "other")
        {
            // Non-actionable fiscal evidence is intentionally excluded from this work queue.
            query = query.Where(x => false);
        }
        else
        {
            query = query.Where(x => x.Direction == ElectronicDocumentDirection.Received &&
                x.PurchaseId == null &&
                (x.DocumentType == ElectronicDocumentType.Invoice ||
                 x.DocumentType == ElectronicDocumentType.ElectronicTicket ||
                 x.DocumentType == ElectronicDocumentType.PurchaseInvoice ||
                 x.DocumentType == ElectronicDocumentType.ExportInvoice) &&
                db.Purchases.Any(p =>
                    p.Supplier.IdentificationType == x.IssuerIdentificationType &&
                    p.Supplier.IdentificationNumber == x.IssuerIdentification &&
                    ((x.CurrencyCode == "CRC" && p.Currency == PurchaseCurrency.CRC) ||
                     (x.CurrencyCode == "USD" && p.Currency == PurchaseCurrency.USD)) &&
                    !db.ElectronicDocuments.Any(e => e.PurchaseId == p.Id)));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.IssuerName.Contains(term) || x.Clave.Contains(term) || x.NumeroConsecutivo.Contains(term));
        }
        // Notes are a diagnostic work queue: do not hide notes with missing or
        // incompatible references. ReviewAsync explains each status to the user.
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.FechaEmision).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new FiscalWorklistItem(x.Id, x.DocumentType.ToString(), x.IssuerName, x.Clave,
                x.FechaEmision, x.CurrencyCode, x.TotalDocument, x.PurchaseId,
                x.PurchaseId != null ? "Linked" : x.DocumentType == ElectronicDocumentType.CreditNote ||
                    x.DocumentType == ElectronicDocumentType.DebitNote ? "ReferenceReview" : "Unlinked"))
            .ToListAsync(ct);
        return new(items, count, page, pageSize);
    }
    public async Task<FiscalPurchaseCandidatesResponse> FindAsync(int id, int days, CancellationToken ct)
    {
        if (days is not (30 or 90 or 3650)) throw new ArgumentOutOfRangeException(nameof(days));
        var doc = await db.ElectronicDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("No existe el documento fiscal.");
        if (doc.Direction != ElectronicDocumentDirection.Received || doc.PurchaseId.HasValue ||
            doc.DocumentType is not (ElectronicDocumentType.Invoice or ElectronicDocumentType.ElectronicTicket or
                ElectronicDocumentType.PurchaseInvoice or ElectronicDocumentType.ExportInvoice))
            throw new InvalidOperationException("El documento no está disponible para vinculación con una compra.");
        var date = DateOnly.FromDateTime(doc.FechaEmision.Date);
        var start = date.AddDays(-days); var end = date.AddDays(days);
        if (!Enum.TryParse<PurchaseCurrency>(doc.CurrencyCode, true, out var currency))
            return new(id, doc.IssuerName, doc.CurrencyCode, doc.TotalNetSale, doc.TotalDocument, []);
        var purchases = await db.Purchases.AsNoTracking()
            .Where(p => p.PurchaseDate >= start && p.PurchaseDate <= end && p.Currency == currency &&
                p.Supplier.IdentificationType == doc.IssuerIdentificationType &&
                p.Supplier.IdentificationNumber == doc.IssuerIdentification &&
                !db.ElectronicDocuments.Any(e => e.PurchaseId == p.Id))
            .Select(p => new { p.Id, p.PurchaseDate, Name = p.Supplier.Name,
                Cost = p.Lines.Sum(l => (decimal?)(l.Quantity * l.UnitCost)) ?? 0m })
            .ToListAsync(ct);
        var results = purchases.Select(p =>
        {
            var apart = Math.Abs(p.PurchaseDate.DayNumber - date.DayNumber);
            var diff = doc.TotalNetSale - p.Cost;
            var reasons = new List<string> { "Identificación fiscal y moneda coinciden" };
            if (apart == 0) reasons.Add("Misma fecha");
            else reasons.Add($"Diferencia de {apart} día(s)");
            if (Math.Abs(diff) <= 1m) reasons.Add("Importe neto cercano al costo de líneas");
            else reasons.Add("Revisar impuestos, descuentos y costo de líneas");
            return new FiscalPurchaseCandidate(p.Id, p.PurchaseDate, p.Name, doc.CurrencyCode,
                p.Cost, diff, apart, reasons);
        }).OrderBy(x => x.DaysApart).ThenBy(x => Math.Abs(x.NetDifference)).ThenBy(x => x.PurchaseId).Take(30).ToArray();
        return new(id, doc.IssuerName, doc.CurrencyCode, doc.TotalNetSale, doc.TotalDocument, results);
    }
    public async Task<FiscalNoteReferencesResponse> NoteAsync(int id, CancellationToken ct)
    {
        var note = await db.ElectronicDocuments.AsNoTracking().Include(x => x.References)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Nota fiscal no encontrada.");
        if (note.DocumentType is not (ElectronicDocumentType.CreditNote or ElectronicDocumentType.DebitNote))
            throw new InvalidOperationException("El documento no es una nota fiscal.");
        var suggestions = await new ElectronicDocumentReferenceLinkService(db).SuggestAsync(id, ct);
        var mapped = note.References.OrderBy(x => x.Sequence).Select(r =>
        {
            var suggestion = suggestions.FirstOrDefault(s => s.ReferenceId == r.Id);
            return new FiscalNoteReferenceItem(r.Id, r.ReferenceNumber, r.Reason,
                r.RelatedElectronicDocumentId, suggestion?.SuggestedDocumentId,
                r.RelatedElectronicDocumentId.HasValue ? "Linked" : suggestion?.Result ?? "RequiresManualReview");
        }).ToArray();
        // Do not allocate a note amount across multiple references. A single
        // reference with a verified matching original permits a documentary preview.
        FiscalNoteAdjustmentPreview? preview = null;
        if (mapped.Length == 1 && note.AdjustmentStatus == ElectronicDocumentAdjustmentStatus.PendingReview)
        {
            var originalId = mapped[0].LinkedDocumentId ?? mapped[0].SuggestedDocumentId;
            if (originalId.HasValue)
            {
                // Use the same approved/applied accumulator as the adjusted-totals endpoint.
                var approved = await new FiscalApprovalService(db)
                    .GetAdjustedTotalsAsync([originalId.Value], ct);
                var total = approved.SingleOrDefault();
                // A persisted link is not proof of ongoing compatibility. Revalidate
                // before showing a projected value that the user might approve.
                var original = await db.ElectronicDocuments.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == originalId.Value, ct);
                if (total is not null && original is not null &&
                    ElectronicDocumentReferenceCompatibilityEvaluator
                        .Evaluate(note, note.References.Single(), original).Count == 0)
                {
                    preview = FiscalAdjustmentCalculator.Project(total,
                        new FiscalAdjustmentCalculator.Note(note.Id, note.Clave,
                            note.DocumentType, note.CurrencyCode, note.TotalDocument));
                }
            }
        }
        var matchIds = mapped.Select(x => x.LinkedDocumentId ?? x.SuggestedDocumentId)
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var matches = await db.ElectronicDocuments.AsNoTracking()
            .Where(x => matchIds.Contains(x.Id))
            .Select(x => new FiscalMatchedDocument(x.Id, x.Clave, x.NumeroConsecutivo,
                x.DocumentType.ToString(), x.IssuerName, x.IssuerIdentification,
                x.FechaEmision, x.CurrencyCode, x.TotalDocument)).ToListAsync(ct);
        // Read-only diagnostics: reveal an imported document matching the fiscal key even
        // when strict linkage rejects it. These candidates cannot be approved by the UI.
        var diagnostics = new List<FiscalReferenceCandidateDiagnostic>();
        foreach (var reference in note.References.OrderBy(x => x.Sequence))
        {
            var number = reference.ReferenceNumber.Trim();
            if (number.Length != 50 || !number.All(char.IsAsciiDigit)) continue;
            var candidates = await db.ElectronicDocuments.AsNoTracking()
                .Where(x => x.Clave == number && x.Id != note.Id)
                .Select(x => new
                {
                    x.Id, x.Clave, x.NumeroConsecutivo, x.DocumentType,
                    x.IssuerName, x.IssuerIdentification, x.IssuerIdentificationType,
                    x.ReceiverIdentification, x.ReceiverIdentificationType,
                    x.Direction, x.FechaEmision, x.CurrencyCode, x.TotalDocument
                }).ToListAsync(ct);
            foreach (var original in candidates)
            {
                var candidateDocument = new Revestik.Api.Models.ElectronicDocument
                {
                    Id = original.Id,
                    Clave = original.Clave,
                    NumeroConsecutivo = original.NumeroConsecutivo,
                    DocumentType = original.DocumentType,
                    IssuerIdentification = original.IssuerIdentification,
                    IssuerIdentificationType = original.IssuerIdentificationType,
                    ReceiverIdentification = original.ReceiverIdentification,
                    ReceiverIdentificationType = original.ReceiverIdentificationType,
                    Direction = original.Direction,
                    FechaEmision = original.FechaEmision,
                    CurrencyCode = original.CurrencyCode
                };
                var reasons = ElectronicDocumentReferenceCompatibilityEvaluator
                    .Evaluate(note, reference, candidateDocument).ToList();
                var mappedReference = mapped.First(x => x.ReferenceId == reference.Id);
                var eligible = (mappedReference.LinkedDocumentId ?? mappedReference.SuggestedDocumentId) == original.Id;
                if (!eligible && reasons.Count == 0)
                    reasons.Add("La vinculación no está habilitada por las validaciones fiscales actuales");
                diagnostics.Add(new FiscalReferenceCandidateDiagnostic(reference.Id,
                    new FiscalMatchedDocument(original.Id, original.Clave, original.NumeroConsecutivo,
                        original.DocumentType.ToString(), original.IssuerName, original.IssuerIdentification,
                        original.FechaEmision, original.CurrencyCode, original.TotalDocument),
                    eligible && reasons.Count == 0, reasons));
            }
        }
        return new FiscalNoteReferencesResponse(id, mapped)
        { Preview = preview, Matches = matches, Diagnostics = diagnostics };
    }
}
