using NexaHR.Application.DTOs.CvAiAnalysis;

namespace NexaHR.Application.Interfaces.CvAiAnalysis;

public interface ICvAiAnalysisService
{
    // API-092
    Task<CvAiOperationResponse<AiConfigurationStatusResponse>>
        GetConfigurationStatusAsync(
            Guid actorUserId,
            CancellationToken cancellationToken);

    // API-093
    Task<CvAiOperationResponse<AiInputResponse>>
        GetPreparedInputAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken);

    // API-094
    Task<CvAiOperationResponse<CvAiAnalysisResponse>>
        StartAsync(
            Guid actorUserId,
            Guid applicationId,
            StartCvAiAnalysisRequest request,
            CancellationToken cancellationToken);

    // API-095
    Task<CvAiOperationResponse<
        IReadOnlyList<CvAiAnalysisResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken);

    // API-096
    Task<CvAiOperationResponse<CvAiAnalysisResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid analysisId,
            CancellationToken cancellationToken);
}
