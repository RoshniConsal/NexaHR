using NexaHR.Application.DTOs.CvReviews;

namespace NexaHR.Application.Interfaces.CvReviews;

public interface ICvReviewService
{
    // API-079: Retrieve the HR-only CV review workspace.
    Task<CvReviewWorkspaceOperationResponse>
        GetWorkspaceAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default);

    // API-080: Save an immutable CV review draft.
    Task<CvReviewDraftOperationResponse> SaveDraftAsync(
        Guid actorUserId,
        Guid applicationId,
        CvReviewDraftRequest request,
        CancellationToken cancellationToken = default);

    // API-081: Confirm the exact latest saved review draft.
    Task<CvReviewConfirmationOperationResponse>
        ConfirmReviewAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewConfirmationRequest request,
            CancellationToken cancellationToken = default);

    // API-082: Retrieve immutable CV review history.
    Task<CvReviewHistoryOperationResponse>
        GetReviewHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewHistoryQuery query,
            CancellationToken cancellationToken = default);

    // API-083: Explicitly start re-review against
    // a selected approved vacancy criteria revision.
    Task<CvReviewRevalidationOperationResponse>
        RevalidateReviewAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewRevalidationRequest request,
            CancellationToken cancellationToken = default);
}
