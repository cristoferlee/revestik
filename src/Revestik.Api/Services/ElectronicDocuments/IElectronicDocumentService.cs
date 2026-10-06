using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public interface IElectronicDocumentService
{
    Task<ElectronicDocumentListResponse> GetPageAsync(
        ElectronicDocumentListRequest request,
        CancellationToken cancellationToken);

    Task<ElectronicDocumentSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken);

    Task<ElectronicDocumentDetailResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<byte[]?> GetOriginalXmlAsync(
        int id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ElectronicDocumentCategoryResponse>> GetCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ElectronicDocumentCategoryResponse> CreateCategoryAsync(
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken);

    Task<ElectronicDocumentCategoryResponse?> UpdateCategoryAsync(
        int id,
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken);

    Task<ElectronicDocumentDetailResponse?> ClassifyAsync(
        int id,
        ElectronicDocumentClassifyRequest request,
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ElectronicDocumentLearningRuleResponse>> GetLearningRulesAsync(
        CancellationToken cancellationToken);

    Task<ElectronicDocumentLearningRuleResponse?> UpdateLearningRuleAsync(
        ElectronicDocumentLearningRuleUpdateRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteLearningRuleAsync(
        string ruleType,
        int ruleId,
        CancellationToken cancellationToken);
}
