using Revestik.Api.Models.Identity;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Models;

public sealed class ElectronicDocument
{
    public int Id { get; set; }

    public string Clave { get; set; } = string.Empty;
    public ElectronicDocumentType DocumentType { get; set; }
    public string NumeroConsecutivo { get; set; } = string.Empty;
    public DateTimeOffset FechaEmision { get; set; }
    public string IssuerEconomicActivityCode { get; set; } = string.Empty;
    public string ReceiverEconomicActivityCode { get; set; } = string.Empty;

    public string IssuerName { get; set; } = string.Empty;
    public string IssuerCommercialName { get; set; } = string.Empty;
    public string IssuerIdentificationType { get; set; } = string.Empty;
    public string IssuerIdentification { get; set; } = string.Empty;
    public string IssuerPhoneNumber { get; set; } = string.Empty;
    public string IssuerEmail { get; set; } = string.Empty;
    public string IssuerAddress { get; set; } = string.Empty;

    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverIdentificationType { get; set; } = string.Empty;
    public string ReceiverIdentification { get; set; } = string.Empty;

    public string SaleConditionCode { get; set; } = string.Empty;
    public int? CreditTermDays { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }

    public decimal TotalTaxedServices { get; set; }
    public decimal TotalExemptServices { get; set; }
    public decimal TotalExoneratedServices { get; set; }
    public decimal TotalNonSubjectServices { get; set; }
    public decimal TotalTaxedGoods { get; set; }
    public decimal TotalExemptGoods { get; set; }
    public decimal TotalExoneratedGoods { get; set; }
    public decimal TotalNonSubjectGoods { get; set; }
    public decimal TotalTaxed { get; set; }
    public decimal TotalExempt { get; set; }
    public decimal TotalExonerated { get; set; }
    public decimal TotalNonSubject { get; set; }
    public decimal TotalSale { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalNetSale { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalVatReturned { get; set; }
    public decimal TotalOtherCharges { get; set; }
    public decimal TotalDocument { get; set; }

    public ElectronicDocumentProcessingStatus ProcessingStatus { get; set; } =
        ElectronicDocumentProcessingStatus.Pending;

    public int? CategoryId { get; set; }
    public ElectronicDocumentCategory? Category { get; set; }
    public OperationalDestination? OperationalDestination { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public byte[] OriginalXml { get; set; } = [];

    public string ImportedByUserId { get; set; } = string.Empty;
    public ApplicationUser ImportedByUser { get; set; } = null!;
    public DateTime ImportedAtUtc { get; set; }

    public string? ProcessedByUserId { get; set; }
    public ApplicationUser? ProcessedByUser { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }

    public ICollection<ElectronicDocumentLine> Lines { get; set; } = [];
    public HaciendaResponse? HaciendaResponse { get; set; }
}
