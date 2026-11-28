using NexaHR.Application.DTOs.Applications;

namespace NexaHR.Application.Interfaces.Applications;

public interface IApplicationService
{

    // API-077: Candidate-owned application withdrawal.
    Task<ApplicationWithdrawalOperationResponse>
        WithdrawAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationWithdrawalRequest request,
            CancellationToken cancellationToken = default);

    Task<ApplicationListOperationResponse> GetApplicationsAsync(
        Guid actorUserId,
        ApplicationQuery request,
        CancellationToken cancellationToken = default);

    Task<ApplicationDetailsOperationResponse> GetApplicationDetailsAsync(
        Guid actorUserId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<ApplicationTimelineOperationResponse> GetApplicationTimelineAsync(
        Guid actorUserId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<ApplicationStageTransitionOperationResponse> TransitionStageAsync(
        Guid actorUserId,
        Guid applicationId,
        ApplicationStageTransitionRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationAiConsentOperationResponse> UpdateAiConsentAsync(
        Guid actorUserId,
        Guid applicationId,
        ApplicationAiConsentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationOperationResponse> SubmitAsync(
        Guid actorUserId,
        SubmitApplicationRequest request,
        CancellationToken cancellationToken = default);
}
