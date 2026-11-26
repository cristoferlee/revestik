namespace Revestik.Shared.Users;

public sealed record PublicCustomerAccountResponse(
    string Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    string Version);

public sealed record PublicCustomerListResponse(
    IReadOnlyList<PublicCustomerAccountResponse> Customers,
    int Page,
    int PageSize,
    int TotalCount);

public sealed class UpdatePublicCustomerStatusRequest
{
    public bool? IsActive { get; set; }

    public string Version { get; set; } = string.Empty;
}
