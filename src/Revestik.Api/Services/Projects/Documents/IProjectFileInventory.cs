namespace Revestik.Api.Services.Projects.Documents;

// Optional provider capability. An Azure implementation can use blob properties
// (LastModified) without changing the document service or persistence model.
public interface IProjectFileInventory
{
    Task<IReadOnlyList<string>> ListKeysOlderThanAsync(DateTime cutoffUtc,
        CancellationToken cancellationToken);
}
