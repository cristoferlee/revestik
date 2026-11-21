namespace Revestik.Shared.PublicSite;

// The browser supplies only public product identifiers, quantities and display language.
// Names, prices, units and publication status are always obtained again on the server.
public sealed record PublicProformaRequest(string? Language, IReadOnlyList<PublicProformaItemRequest>? Items);
public sealed record PublicProformaItemRequest(int ProductId, decimal Quantity);
