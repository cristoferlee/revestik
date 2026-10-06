using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public interface IReceivedDocumentInboxService
{
    Task<ReceivedDocumentInboxItemResponse> StageAsync(
        byte[] xml,
        string fileName,
        string source,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReceivedDocumentInboxItemResponse>> GetPendingAsync(
        CancellationToken cancellationToken);

    Task<ReceivedDocumentInboxAcceptResponse> AcceptAsync(
        Guid inboxId,
        string userId,
        CancellationToken cancellationToken);

    Task<bool> RejectAsync(
        Guid inboxId,
        CancellationToken cancellationToken);
}
