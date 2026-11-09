namespace Revestik.Shared.Products;

public sealed record ProductSubcategoryResponse(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string Version);
