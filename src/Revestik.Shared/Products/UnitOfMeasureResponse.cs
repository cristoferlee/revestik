namespace Revestik.Shared.Products;

public sealed record UnitOfMeasureResponse(
    int Id,
    string Name,
    string Symbol,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);