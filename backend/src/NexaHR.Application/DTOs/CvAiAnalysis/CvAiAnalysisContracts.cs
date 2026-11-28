using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NexaHR.Application.DTOs.CvAiAnalysis;

public sealed record CvAiOperationResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data)
    where T : class;

public sealed record AiConsentNoticeResponse(
    string Provider,
    Guid ProcessingPolicyId,
    int PolicyVersion,
    string TextVersion,
    string ProcessingPurpose,
    string DataCategories,
    string ProviderRetention,
    bool ProviderTrainingAllowed,
    string ProviderAccessPolicy,
    string ProcessingLocation,
    bool IsOptional);

public sealed record AiConfigurationStatusResponse(
    bool Enabled,
    bool Ready,
    string ReadinessCode,
    string? Provider,
    string? Model,
    string? ModelDigest,
    string? PromptVersion,
    int? InputSchemaVersion,
    int? OutputSchemaVersion,
    AiConsentNoticeResponse? ConsentNotice,
    bool ManualReviewAvailable,
    bool RuleBasedMatchingAvailable);

public sealed record AiInputResponse(
    Guid ApplicationId,
    Guid DocumentId,
    Guid VacancyRevisionId,
    Guid CvReviewVersionId,
    Guid CvMatchRunId,
    Guid? ProcessingPolicyId,
    Guid? ApplicationConsentId,
    string? Provider,
    string? Model,
    string? ModelDigest,
    string SourceSha256Hash,
    string InputSha256Hash,
    string PromptVersion,
    int InputSchemaVersion,
    int OutputSchemaVersion,
    AiPreparedInput Input,
    IReadOnlyList<AiCriterionReference> CriterionReferences,
    IReadOnlyList<AiEvidenceReference> EvidenceReferences,
    bool CanStartAnalysis,
    string AuthorizationCode,
    bool RequiresHumanReview);

[JsonUnmappedMemberHandling(
    JsonUnmappedMemberHandling.Disallow)]
public sealed class StartCvAiAnalysisRequest
{
    [Required]
    public Guid DocumentId { get; init; }

    [Required]
    public Guid VacancyRevisionId { get; init; }

    [Required]
    public Guid CvReviewVersionId { get; init; }

    [Required]
    public Guid CvMatchRunId { get; init; }

    [Required]
    public Guid ProcessingPolicyId { get; init; }

    [Required]
    public Guid ApplicationConsentId { get; init; }

    [Required]
    [RegularExpression(
        @"\A[A-Fa-f0-9]{64}\z")]
    public string InputSha256Hash { get; init; } = "";

    [Required]
    [StringLength(200)]
    public string Provider { get; init; } = "";

    [Required]
    [StringLength(200)]
    public string Model { get; init; } = "";

    [Required]
    [RegularExpression(
        @"\A[A-Fa-f0-9]{64}\z")]
    public string ModelDigest { get; init; } = "";

    [Required]
    [StringLength(100)]
    public string PromptVersion { get; init; } = "";

    [Range(1, 1)]
    public int InputSchemaVersion { get; init; }

    [Range(1, 1)]
    public int OutputSchemaVersion { get; init; }
}

public sealed record CvAiAnalysisResponse(
    Guid Id,
    Guid ApplicationId,
    Guid DocumentId,
    Guid VacancyRevisionId,
    Guid CvReviewVersionId,
    Guid CvMatchRunId,
    Guid ProcessingPolicyId,
    Guid ApplicationConsentId,
    int Version,
    string Provider,
    string Model,
    string ModelDigest,
    string PromptVersion,
    int InputSchemaVersion,
    int OutputSchemaVersion,
    string SourceSha256Hash,
    string InputSha256Hash,
    string Status,
    bool IsStale,
    IReadOnlyList<string> StaleReasons,
    int AttemptCount,
    string? FailureCode,
    Guid RequestedByUserId,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    bool RequiresHumanReview,
    AiPreparedInput? Input,
    AiValidatedOutput? Result,
    IReadOnlyList<AiCriterionReference>? CriterionReferences,
    IReadOnlyList<AiEvidenceReference>? EvidenceReferences);
