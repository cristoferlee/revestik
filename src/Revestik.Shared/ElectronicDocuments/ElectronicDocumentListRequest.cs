using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.ElectronicDocuments;

public sealed class ElectronicDocumentListRequest
{
    public string? Search { get; set; }
    public ElectronicDocumentType? DocumentType { get; set; }
    public ElectronicDocumentProcessingStatus? ProcessingStatus { get; set; }
    public int? CategoryId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
