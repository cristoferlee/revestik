using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalAdjustmentCalculatorTests
{
    private static FiscalAdjustmentCalculator.Note Note(int id, ElectronicDocumentType type,
        decimal amount, string currency = "CRC") => new(id, $"note-{id}", type, currency, amount);

    [Fact]
    public void SeveralCreditsAndDebits_AreAccumulatedOnlyOnce()
    {
        var approved = FiscalAdjustmentCalculator.Calculate(1, "CRC", 150000m,
        [
            Note(2, ElectronicDocumentType.CreditNote, 30000m),
            Note(3, ElectronicDocumentType.CreditNote, 10000m),
            Note(4, ElectronicDocumentType.DebitNote, 5000m)
        ]);
        Assert.Equal(40000m, approved.CreditNotesTotal);
        Assert.Equal(5000m, approved.DebitNotesTotal);
        Assert.Equal(115000m, approved.AdjustedTotal);
        var preview = FiscalAdjustmentCalculator.Project(approved,
            Note(5, ElectronicDocumentType.CreditNote, 20000m));
        Assert.Equal(115000m, preview.CurrentAdjustedTotal);
        Assert.Equal(-20000m, preview.NoteDelta);
        Assert.Equal(95000m, preview.ProjectedTotal);
        Assert.Equal(150000m, preview.OriginalTotal);
    }

    [Fact]
    public void DebitPreview_AccumulatesFromApprovedAmount()
    {
        var approved = FiscalAdjustmentCalculator.Calculate(1, "USD", 100m,
            [Note(2, ElectronicDocumentType.CreditNote, 20m, "USD")]);
        var preview = FiscalAdjustmentCalculator.Project(approved,
            Note(3, ElectronicDocumentType.DebitNote, 7.12567m, "USD"));
        Assert.Equal(87.12567m, preview.ProjectedTotal);
    }

    [Fact]
    public void FiveDecimalPrecision_IsPreservedWithoutPerNoteRounding()
    {
        var result = FiscalAdjustmentCalculator.Calculate(1, "CRC", 10.00001m,
            [Note(2, ElectronicDocumentType.CreditNote, .00002m),
             Note(3, ElectronicDocumentType.DebitNote, .00003m)]);
        Assert.Equal(10.00002m, result.AdjustedTotal);
    }

    [Fact]
    public void NegativeProjection_IsPotentialExcessNotAnEconomicCredit()
    {
        var approved = FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m, []);
        var projected = FiscalAdjustmentCalculator.Project(approved,
            Note(2, ElectronicDocumentType.CreditNote, 130m));
        Assert.Equal(-30m, projected.ProjectedTotal);
        Assert.Equal(30m, projected.PotentialExcess);
    }

    [Fact]
    public void DuplicateApprovedNote_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m,
            [Note(2, ElectronicDocumentType.CreditNote, 10m),
             Note(2, ElectronicDocumentType.CreditNote, 10m)]));
    }

    [Fact]
    public void AlreadyApprovedCandidate_CannotBeProjectedTwice()
    {
        var approved = FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m,
            [Note(2, ElectronicDocumentType.CreditNote, 10m)]);
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Project(approved,
            Note(2, ElectronicDocumentType.CreditNote, 10m)));
    }

    [Fact]
    public void DifferentCurrency_IsRejectedInCalculationAndProjection()
    {
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m,
            [Note(2, ElectronicDocumentType.CreditNote, 10m, "USD")]));
        var approved = FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m, []);
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Project(approved,
            Note(2, ElectronicDocumentType.DebitNote, 10m, "USD")));
    }

    [Fact]
    public void InvalidTypeAndNegativeAmount_AreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m,
            [Note(2, ElectronicDocumentType.Invoice, 10m)]));
        Assert.Throws<InvalidOperationException>(() => FiscalAdjustmentCalculator.Calculate(1, "CRC", 100m,
            [Note(2, ElectronicDocumentType.CreditNote, -10m)]));
    }
}
