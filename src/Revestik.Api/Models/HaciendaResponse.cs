namespace Revestik.Api.Models;

public sealed class HaciendaResponse
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;

    public string IssuerName { get; set; } = string.Empty;
    public string IssuerIdentificationType { get; set; } = string.Empty;
    public string IssuerIdentification { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverIdentificationType { get; set; } = string.Empty;
    public string ReceiverIdentification { get; set; } = string.Empty;

    public string MessageCode { get; set; } = string.Empty;
    public string MessageStatus { get; set; } = string.Empty;
    public string MessageDetail { get; set; } = string.Empty;
    public decimal TotalTax { get; set; }
    public decimal TotalInvoice { get; set; }

    public byte[] OriginalXml { get; set; } = [];
    public DateTime ReceivedAtUtc { get; set; }

    public int? ElectronicDocumentId { get; set; }
    public ElectronicDocument? ElectronicDocument { get; set; }
}
