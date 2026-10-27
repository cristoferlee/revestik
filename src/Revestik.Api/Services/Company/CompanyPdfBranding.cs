using Revestik.Api.Services.Company;

namespace Revestik.Api.Services.Company;

// Fully prepared, immutable input for PDF rendering. No database or disk access in QuestPDF.
public sealed record CompanyPdfBranding(
    CompanyDocumentIdentity Identity,
    string? DefaultSvg,
    byte[]? RasterLogo);
