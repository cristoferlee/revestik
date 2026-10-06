using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

internal sealed class ReceivedDocumentInboxEnvelope
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public ReceivedDocumentInboxStatus Status { get; set; }
    public string DocumentKind { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string IssuerName { get; set; } = string.Empty;
    public string IssuerIdentification { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal TotalCrcEquivalent { get; set; }
    public bool RequiresHighAmountReview { get; set; }
    public List<string> Warnings { get; set; } = [];
    public int? ExistingElectronicDocumentId { get; set; }
    public int? AcceptedElectronicDocumentId { get; set; }
    public string XmlBase64 { get; set; } = string.Empty;
}
