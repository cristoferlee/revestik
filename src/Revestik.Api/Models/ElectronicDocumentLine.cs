using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Models;

public sealed class ElectronicDocumentLine
{
    public int Id { get; set; }

    public int ElectronicDocumentId { get; set; }
    public ElectronicDocument ElectronicDocument { get; set; } = null!;

    public int LineNumber { get; set; }
    public string CabysCode { get; set; } = string.Empty;
    public string CommercialCodeType { get; set; } = string.Empty;
    public string CommercialCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string CommercialUnitOfMeasure { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal NetTax { get; set; }
    public decimal TotalLine { get; set; }

    public int? ClassificationCategoryId { get; set; }
    public ElectronicDocumentCategory? ClassificationCategory { get; set; }
    public OperationalDestination? OperationalDestination { get; set; }

    public ICollection<ElectronicDocumentLineDiscount> Discounts { get; set; } = [];
    public ICollection<ElectronicDocumentLineTax> Taxes { get; set; } = [];
}
