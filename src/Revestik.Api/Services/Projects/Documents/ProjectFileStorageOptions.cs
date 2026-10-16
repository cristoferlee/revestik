namespace Revestik.Api.Services.Projects.Documents;

public sealed class ProjectFileStorageOptions
{
    public const string SectionName = "ProjectFileStorage";
    public string RootPath { get; set; } = string.Empty;
}
