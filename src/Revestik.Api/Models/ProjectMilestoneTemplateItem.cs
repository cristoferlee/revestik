namespace Revestik.Api.Models;

public sealed class ProjectMilestoneTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public ProjectMilestoneTemplate Template { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }
}
