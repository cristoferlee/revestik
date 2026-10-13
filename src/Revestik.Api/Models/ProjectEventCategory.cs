namespace Revestik.Api.Models;

public sealed class ProjectEventCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ProjectEvent> Events { get; set; } = [];
}
