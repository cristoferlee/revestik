using Revestik.Api.Models.Identity;
using Revestik.Shared.Projects;

namespace Revestik.Api.Models;

public sealed class Project
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public long Version { get; set; } = 1;
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResponsibleUserId { get; set; } = string.Empty;
    public ApplicationUser ResponsibleUser { get; set; } = null!;
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public string? ProvinceCode { get; set; }
    public string? CantonCode { get; set; }
    public string? DistrictCode { get; set; }
    public string? OtherSigns { get; set; }
    public DateOnly? EstimatedStartDate { get; set; }
    public DateOnly? EstimatedEndDate { get; set; }
    public decimal? AgreedAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<ProjectContact> Contacts { get; set; } = [];
}
