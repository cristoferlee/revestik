using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public interface IElectronicDocumentImportService
{
    Task<ElectronicDocumentImportResponse> ImportAsync(
        byte[] xml,
        string importedByUserId,
        CancellationToken cancellationToken);
}
