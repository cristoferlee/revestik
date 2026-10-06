using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Client.Services.ElectronicDocuments;

public interface IElectronicDocumentApiService
{
    Task<ElectronicDocumentListResponse> GetPageAsync(
        ElectronicDocumentListRequest request,
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentDetailResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ElectronicDocumentCategoryResponse>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentCategoryResponse> CreateCategoryAsync(
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentCategoryResponse?> UpdateCategoryAsync(
        int id,
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentDetailResponse?> ClassifyAsync(
        int id,
        ElectronicDocumentClassifyRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceivedDocumentInboxItemResponse> ImportAsync(
        Stream xmlStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceivedDocumentInboxItemResponse>> GetInboxAsync(
        CancellationToken cancellationToken = default);

    Task<ReceivedDocumentInboxAcceptResponse> AcceptInboxAsync(
        Guid inboxId,
        CancellationToken cancellationToken = default);

    Task<bool> RejectInboxAsync(
        Guid inboxId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ElectronicDocumentLearningRuleResponse>> GetLearningRulesAsync(
        CancellationToken cancellationToken = default);

    Task<ElectronicDocumentLearningRuleResponse?> UpdateLearningRuleAsync(
        ElectronicDocumentLearningRuleUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteLearningRuleAsync(
        string ruleType,
        int ruleId,
        CancellationToken cancellationToken = default);
}
