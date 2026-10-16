namespace Revestik.Shared.Projects;

public sealed record ProjectDocumentListItem(int Id, int ProjectId, string DisplayName,
    string Category, int? CurrentVersionId, long Version, DateTime CreatedAtUtc);

public sealed record ProjectDocumentVersionItem(int Id, int Number, string OriginalFileName,
    string ContentType, long SizeBytes, string Sha256Hex, DateTime UploadedAtUtc,
    string UploadedByUserId, bool IsCurrent);

public sealed record ProjectDocumentDetails(ProjectDocumentListItem Document,
    IReadOnlyList<ProjectDocumentVersionItem> Versions);
