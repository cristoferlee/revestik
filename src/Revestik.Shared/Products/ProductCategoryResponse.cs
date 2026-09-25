namespace Revestik.Shared.Products;

public sealed record ProductCategoryResponse(
    int Id,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);