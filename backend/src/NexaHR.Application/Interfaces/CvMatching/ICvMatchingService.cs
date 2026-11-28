using NexaHR.Application.DTOs.CvMatching;

namespace NexaHR.Application.Interfaces.CvMatching;

public interface ICvMatchingService
{
    Task<CvMatchingOperationResponse<CvMatchResponse>>
        CreateAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken);

    Task<CvMatchingOperationResponse<
        IReadOnlyList<CvMatchResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken);

    Task<CvMatchingOperationResponse<CvMatchResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid matchId,
            CancellationToken cancellationToken);
}
