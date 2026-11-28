namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationDetailsResponse
{
    public Guid ApplicationId { get; init; }

    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid DocumentId { get; init; }

    public string JobTitle { get; init; }
        = string.Empty;

    public string DepartmentName { get; init; }
        = string.Empty;

    public string Stage { get; init; }
        = string.Empty;

    public DateTime AppliedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    // HR-only fields.
    public Guid? CandidateUserId { get; init; }

    public string? CandidateName { get; init; }

    // HR or owner Candidate concurrency token.
    public string? RowVersion { get; init; }

    // Owner-only recorded AI consent information.
    public IReadOnlyList<ApplicationAiConsentResponse>?
        AiConsents { get; init; }

    // Owner Candidate only.
    public ApplicationAiConsentPolicyResponse?
        AiConsentPolicy { get; init; }
}

public sealed class ApplicationAiConsentResponse
{
    public Guid ConsentId { get; init; }

    public string Provider { get; init; }
        = string.Empty;

    public Guid ProcessingPolicyId { get; init; }

    public string TextVersion { get; init; }
        = string.Empty;

    public bool Granted { get; init; }

    public DateTime AtUtc { get; init; }
}


public sealed class ApplicationAiConsentPolicyResponse
{
    public bool IsConfigured { get; init; }

    public bool CanGrant { get; init; }

    public bool CanRevoke { get; init; }

    public bool RealDataProcessingEnabled { get; init; }

    public Guid? ProcessingPolicyId { get; init; }

    public int? PolicyVersion { get; init; }

    public string? Provider { get; init; }

    public string? TextVersion { get; init; }

    public string? ConsentText { get; init; }

    public bool Granted { get; init; }

    public Guid? LatestConsentId { get; init; }

    public DateTime? LatestConsentAtUtc { get; init; }
}

public sealed class ApplicationDetailsOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationDetailsResponse? Data { get; init; }
}
