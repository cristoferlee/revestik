namespace Revestik.Shared.Common;

public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages =>
        (int)Math.Ceiling(TotalCount / (double)PageSize);
}