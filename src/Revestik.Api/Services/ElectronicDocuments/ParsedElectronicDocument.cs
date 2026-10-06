using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed record ParsedElectronicDocument(
    ElectronicDocumentType DocumentType,
    string Clave,
    string NumeroConsecutivo,
    DateTimeOffset FechaEmision,
    string IssuerEconomicActivityCode,
    string ReceiverEconomicActivityCode,
    ParsedParty Issuer,
    ParsedParty Receiver,
    string SaleConditionCode,
    int? CreditTermDays,
    string CurrencyCode,
    decimal ExchangeRate,
    ParsedTotals Totals,
    IReadOnlyList<ParsedLine> Lines);

public sealed record ParsedHaciendaResponse(
    string Clave,
    string IssuerName,
    string IssuerIdentificationType,
    string IssuerIdentification,
    string ReceiverName,
    string ReceiverIdentificationType,
    string ReceiverIdentification,
    string MessageCode,
    string MessageStatus,
    string MessageDetail,
    decimal TotalTax,
    decimal TotalInvoice);

public abstract record ParsedReceivedXml;
public sealed record ParsedReceivedElectronicDocument(ParsedElectronicDocument Document) : ParsedReceivedXml;
public sealed record ParsedReceivedHaciendaResponse(ParsedHaciendaResponse Response) : ParsedReceivedXml;

public sealed record ParsedParty(
    string Name,
    string CommercialName,
    string IdentificationType,
    string Identification,
    string PhoneNumber,
    string Email,
    string Address);

public sealed record ParsedLine(
    int LineNumber,
    string CabysCode,
    string CommercialCodeType,
    string CommercialCode,
    decimal Quantity,
    string UnitOfMeasure,
    string CommercialUnitOfMeasure,
    string Description,
    decimal UnitPrice,
    decimal GrossAmount,
    decimal Subtotal,
    decimal TaxableBase,
    decimal NetTax,
    decimal TotalLine,
    IReadOnlyList<ParsedDiscount> Discounts,
    IReadOnlyList<ParsedTax> Taxes);

public sealed record ParsedDiscount(decimal Amount, string Code, string Nature);
public sealed record ParsedTax(string TaxCode, string VatRateCode, decimal Rate, decimal Amount);

public sealed record ParsedTotals(
    decimal TotalTaxedServices,
    decimal TotalExemptServices,
    decimal TotalExoneratedServices,
    decimal TotalNonSubjectServices,
    decimal TotalTaxedGoods,
    decimal TotalExemptGoods,
    decimal TotalExoneratedGoods,
    decimal TotalNonSubjectGoods,
    decimal TotalTaxed,
    decimal TotalExempt,
    decimal TotalExonerated,
    decimal TotalNonSubject,
    decimal TotalSale,
    decimal TotalDiscounts,
    decimal TotalNetSale,
    decimal TotalTax,
    decimal TotalVatReturned,
    decimal TotalOtherCharges,
    decimal TotalDocument);
