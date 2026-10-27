using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Users;

public sealed record ManagedUserResponse(
    string Id, string Email, string DisplayName, string Role,
    bool IsActive, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    string Version);

public sealed record ManagedUserListResponse(IReadOnlyList<ManagedUserResponse> Users);

public sealed class CreateManagedUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateManagedUserRequest
{
    [Required, MaxLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [Required]
    public string Version { get; set; } = string.Empty;
}
