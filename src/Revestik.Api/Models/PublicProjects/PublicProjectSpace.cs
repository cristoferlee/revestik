namespace Revestik.Api.Models.PublicProjects;

// Stable code, not a link to the internal ProductCategory / ProductSubcategory tables.
// The predefined code list may be replaced by a managed public lookup in a later version.
public sealed class PublicProjectSpace
{
    public int ProjectId { get; set; }
    public PublicProject Project { get; set; } = null!;
    public string SpaceCode { get; set; } = string.Empty;
}
