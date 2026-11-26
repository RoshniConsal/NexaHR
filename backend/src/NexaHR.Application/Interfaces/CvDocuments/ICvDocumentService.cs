using NexaHR.Application.DTOs.CvDocuments;

namespace NexaHR.Application.Interfaces.CvDocuments;

public interface ICvDocumentService
{
    Task<CvDocumentOperationResponse> UploadAsync(
        Guid actorUserId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<CvDocumentListResponse?> GetListAsync(
        Guid actorUserId,
        CvDocumentQuery query,
        CancellationToken cancellationToken = default);

    Task<CvDocumentResponse?> GetDetailsAsync(
        Guid actorUserId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<CvDocumentContentResponse> GetContentAsync(
        Guid actorUserId,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
