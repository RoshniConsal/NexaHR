using NexaHR.Application.DTOs.Policies;

namespace NexaHR.Application.Interfaces.Policies;

public interface IHrPolicyService
{
    // API-044
    Task<PolicyOperationResult<PolicyListResponse>>
        GetPoliciesAsync(
            Guid actorUserId,
            PolicyListQuery query,
            CancellationToken cancellationToken = default);

    // API-045
    Task<PolicyOperationResult<PolicyDetailsResponse>>
        CreatePolicyAsync(
            Guid actorUserId,
            CreatePolicyRequest request,
            CancellationToken cancellationToken = default);

    // API-046
    Task<PolicyOperationResult<PolicyDetailsResponse>>
        GetPolicyAsync(
            Guid actorUserId,
            Guid policyId,
            CancellationToken cancellationToken = default);

    // API-047
    Task<PolicyOperationResult<PolicyDetailsResponse>>
        UpdateDraftAsync(
            Guid actorUserId,
            Guid policyId,
            UpdatePolicyDraftRequest request,
            CancellationToken cancellationToken = default);

    // API-048
    Task<PolicyOperationResult<PolicyVersionResponse>>
        PublishPolicyAsync(
            Guid actorUserId,
            Guid policyId,
            PublishPolicyRequest request,
            CancellationToken cancellationToken = default);

    // API-049
    Task<PolicyOperationResult<PolicyVersionResponse>>
        GetPolicyVersionAsync(
            Guid actorUserId,
            Guid policyId,
            int version,
            CancellationToken cancellationToken = default);

    // API-050
    Task<PolicyOperationResult<PolicyAcknowledgementResponse>>
        AcknowledgePolicyAsync(
            Guid actorUserId,
            Guid policyId,
            int version,
            CancellationToken cancellationToken = default);
}
