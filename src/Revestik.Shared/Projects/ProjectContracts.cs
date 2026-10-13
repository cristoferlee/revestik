using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Projects;

public sealed record ProjectContactInput(
    [property: Required, StringLength(150)] string Name,
    [property: Required, StringLength(100)] string Role,
    [property: StringLength(20)] string? PhoneNumber,
    [property: EmailAddress, StringLength(254)] string? Email);

public sealed record ProjectUpsertRequest(
    [property: Required, StringLength(150)] string Name,
    [property: StringLength(2000)] string? Description,
    string? ProvinceCode,
    string? CantonCode,
    string? DistrictCode,
    [property: StringLength(160)] string? OtherSigns,
    DateOnly? EstimatedStartDate,
    DateOnly? EstimatedEndDate,
    decimal? AgreedAmount,
    string? CurrencyCode,
    IReadOnlyList<ProjectContactInput>? Contacts,
    long? Version = null);

public sealed record ProjectContactResponse(int Id, string Name, string Role, string? PhoneNumber, string? Email);

public sealed record ProjectResponse(
    int Id,
    int CustomerId,
    string Name,
    string Description,
    string ResponsibleUserId,
    ProjectStatus Status,
    string? ProvinceCode,
    string? CantonCode,
    string? DistrictCode,
    string? OtherSigns,
    DateOnly? EstimatedStartDate,
    DateOnly? EstimatedEndDate,
    decimal? AgreedAmount,
    string? CurrencyCode,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<ProjectContactResponse> Contacts,
    long Version);

public sealed record ProjectSummaryResponse(int Id, int CustomerId, string Name,
    ProjectStatus Status, string ResponsibleUserId, DateTime CreatedAtUtc);

public sealed record ProjectReassignRequest([property: Required] string ResponsibleUserId, long Version);
