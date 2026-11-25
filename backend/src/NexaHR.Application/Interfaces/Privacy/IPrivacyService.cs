using NexaHR.Application.DTOs.Privacy;

namespace NexaHR.Application.Interfaces.Privacy;

public interface IPrivacyService
{
    // API-154
    Task<PrivacyOperationResponse<ProcessingPolicyResponse>>
        GetProcessingPolicyAsync(Guid actorId);

    // API-155
    Task<PrivacyOperationResponse<ProcessingPolicyResponse>>
        UpdateProcessingPolicyAsync(
            Guid actorId,
            UpdateProcessingPolicyRequest request);

    // API-156
    Task<PrivacyOperationResponse<RetentionPreviewResponse>>
        PreviewRetentionAsync(
            Guid actorId,
            RetentionPreviewRequest request);

    // API-157
    Task<PrivacyOperationResponse<RetentionRunResponse>>
        CreateRetentionRunAsync(
            Guid actorId,
            CreateRetentionRunRequest request);

    // API-158
    Task<PrivacyOperationResponse<RetentionRunResponse>>
        GetRetentionRunAsync(
            Guid actorId,
            Guid runId);

    // API-159
    Task<PrivacyOperationResponse<LegalHoldResponse>>
        UpsertLegalHoldAsync(
            Guid actorId,
            Guid subjectId,
            UpsertLegalHoldRequest request);

    // API-160
    Task<PrivacyOperationResponse<LegalHoldResponse>>
        ReleaseLegalHoldAsync(
            Guid actorId,
            Guid subjectId,
            ReleaseLegalHoldRequest request);
}
