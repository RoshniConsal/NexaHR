using NexaHR.Application.DTOs.CvExtractions;

namespace NexaHR.Application.Interfaces.CvExtractions;

public interface ICvExtractionService
{
    // API-084
    Task<CvExtractionOperationResponse<CvExtractionResponse>>
        StartAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default);

    // API-085
    Task<CvExtractionOperationResponse<
        IReadOnlyList<CvExtractionResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default);

    // API-086
    Task<CvExtractionOperationResponse<CvExtractionResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid runId,
            CancellationToken cancellationToken = default);
}
