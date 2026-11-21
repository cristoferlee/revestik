namespace Revestik.Shared.PublicSite;

public sealed record PublicCartQuantityWrite(decimal Quantity, string? Version);

// No owner identifier is accepted from or returned to the browser.
public sealed record PublicCartResponse(IReadOnlyList<PublicCartLineResponse> Items);

public sealed record PublicCartLineResponse(
    int ProductId,
    decimal Quantity,
    string Version,
    bool IsAvailable,
    string? Slug,
    string? Brand,
    decimal? Price,
    string? Currency,
    string? SalesUnit,
    int? CoverImageId,
    IReadOnlyList<PublicCartTranslationResponse> Translations);

public sealed record PublicCartTranslationResponse(string LanguageCode, string Name);
