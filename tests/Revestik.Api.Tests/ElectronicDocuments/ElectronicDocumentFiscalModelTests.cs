using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentFiscalModelTests
{
    [Fact]
    public void NewDocument_DefaultsToReceivedWithoutAdjustment()
    {
        var document = new ElectronicDocument();
        Assert.Equal(ElectronicDocumentDirection.Received, document.Direction);
        Assert.Null(document.AdjustmentStatus);
        Assert.Empty(document.References);
    }

    [Fact]
    public void Reference_CanExistWithoutRelatedImportedDocument()
    {
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1,
            ReferencedDocumentTypeCode = "01",
            ReferenceNumber = "50601012600310123456700100001010000000001123456789",
            ReferencedIssueDate = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-6)),
            ReferenceCode = "01",
            RelatedElectronicDocumentId = null
        };

        Assert.Null(reference.RelatedElectronicDocumentId);
        Assert.Equal("01", reference.ReferencedDocumentTypeCode);
    }

    [Fact]
    public void ExistingDocumentTypeNumbers_AreStable()
    {
        Assert.Equal(1, (int)ElectronicDocumentType.Invoice);
        Assert.Equal(2, (int)ElectronicDocumentType.CreditNote);
        Assert.Equal(3, (int)ElectronicDocumentType.DebitNote);
    }
}
