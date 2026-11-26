namespace NexaHR.Application.DTOs.CvDocuments;

public sealed class CvDocumentListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public List<CvDocumentResponse> Items { get; init; }
        = [];
}
