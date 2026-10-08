using Revestik.Api.Services.HaciendaXml;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed record ParsedHaciendaDocument(
    HaciendaXmlDocumentKind Kind,
    string Clave,
    string NumeroConsecutivo,
    DateTimeOffset FechaEmision,
    ParsedHaciendaParty Issuer,
    ParsedHaciendaParty? Receiver,
    string IssuerEconomicActivityCode,
    string ReceiverEconomicActivityCode,
    string SaleConditionCode,
    int? CreditTermDays,
    string CurrencyCode,
    decimal? ExchangeRate,
    decimal TotalDocument,
    IReadOnlyList<ParsedHaciendaLine> Lines,
    IReadOnlyList<ParsedHaciendaReference> References) : ParsedReceivedXml;

public sealed record ParsedHaciendaParty(
    string Name,
    string IdentificationType,
    string Identification,
    string CommercialName,
    string Email);

public sealed record ParsedHaciendaLine(
    int Number,
    string CabysCode,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalLine);

public sealed record ParsedHaciendaReference(
    string DocumentType,
    string Number,
    DateTimeOffset IssueDate,
    string Code,
    string Reason);
