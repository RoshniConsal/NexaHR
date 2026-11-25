namespace NexaHR.Application.DTOs.Privacy;

public sealed class ProcessingPolicyResponse
{
    public bool IsConfigured { get; init; }

    public bool IsRestricted { get; init; }

    public bool RealDataProcessingEnabled { get; init; }

    public Guid? PolicyId { get; init; }

    public int? Version { get; init; }

    public int? CandidateRetentionDays { get; init; }

    public int? EmploymentRetentionDays { get; init; }

    public int? AuditRetentionDays { get; init; }

    public string? CandidateRetentionAction { get; init; }

    public string? EmploymentRetentionAction { get; init; }

    public string? AuditRetentionAction { get; init; }

    public string? ProcessingPurpose { get; init; }

    public string? DataCategories { get; init; }

    public string? ProviderName { get; init; }

    public string? ProviderRetention { get; init; }

    public bool? ProviderTrainingAllowed { get; init; }

    public string? ProviderAccessPolicy { get; init; }

    public string? ProcessingLocation { get; init; }

    public string? LegalHoldPolicy { get; init; }

    public string? ApprovalReference { get; init; }

    public Guid? ApprovedByUserId { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public Guid? RecordedByUserId { get; init; }

    public DateTime? CreatedAtUtc { get; init; }
}

public sealed class RetentionPreviewResponse
{
    public int PolicyVersion { get; init; }

    public string DataCategory { get; init; } = "";

    public string Action { get; init; } = "";

    public DateTime? CutoffUtc { get; init; }

    public int MaxRecords { get; init; }

    public int? EligibleCount { get; init; }

    public int? HeldCount { get; init; }

    public bool CanExecute { get; init; }

    public string Status { get; init; } = "";

    public string? BlockReason { get; init; }
}

public sealed class RetentionRunResponse
{
    public Guid RunId { get; init; }

    public int PolicyVersion { get; init; }

    public string DataCategory { get; init; } = "";

    public string Action { get; init; } = "";

    public string Status { get; init; } = "";

    public int MaxRecords { get; init; }

    public int? EligibleCount { get; init; }

    public int ProcessedCount { get; init; }

    public int? HeldCount { get; init; }

    public int FailedCount { get; init; }

    public DateTime CutoffUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? ResultCode { get; init; }
}

public sealed class LegalHoldResponse
{
    public Guid LegalHoldId { get; init; }

    public Guid SubjectId { get; init; }

    public string Scope { get; init; } = "";

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime? ReleasedAtUtc { get; init; }

    public string? RowVersion { get; init; }
}

public sealed class PrivacyOperationResponse<T>
    where T : class
{
    public bool Success { get; init; }

    public string Code { get; init; } = "";

    public string Message { get; init; } = "";

    public T? Data { get; init; }
}
