using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Privacy;

public sealed class UpdateProcessingPolicyRequest
{
    [Range(0, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [Range(1, 36500)]
    public int CandidateRetentionDays { get; set; }

    [Range(1, 36500)]
    public int EmploymentRetentionDays { get; set; }

    [Range(1, 36500)]
    public int AuditRetentionDays { get; set; }

    [Required]
    [RegularExpression("^(Delete|Anonymise)$")]
    public string CandidateRetentionAction { get; set; } = "";

    [Required]
    [RegularExpression("^(Delete|Anonymise)$")]
    public string EmploymentRetentionAction { get; set; } = "";

    [Required]
    [RegularExpression("^(Delete|Anonymise)$")]
    public string AuditRetentionAction { get; set; } = "";

    [Required, StringLength(2000)]
    public string ProcessingPurpose { get; set; } = "";

    [Required, StringLength(2000)]
    public string DataCategories { get; set; } = "";

    [Required, StringLength(200)]
    public string ProviderName { get; set; } = "";

    [Required, StringLength(1000)]
    public string ProviderRetention { get; set; } = "";

    [Required]
    public bool? ProviderTrainingAllowed { get; set; }

    [Required, StringLength(2000)]
    public string ProviderAccessPolicy { get; set; } = "";

    [Required, StringLength(500)]
    public string ProcessingLocation { get; set; } = "";

    [Required, StringLength(2000)]
    public string LegalHoldPolicy { get; set; } = "";

    [Required, StringLength(500)]
    public string ApprovalReference { get; set; } = "";

    [Required]
    public Guid? ApprovedByUserId { get; set; }

    [Required]
    public DateTimeOffset? ApprovedAtUtc { get; set; }
}

public sealed class RetentionPreviewRequest
{
    [Required]
    [RegularExpression("^(Candidate|Employment|Audit)$")]
    public string DataCategory { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int PolicyVersion { get; set; }

    [Range(1, 1000)]
    public int MaxRecords { get; set; } = 100;
}

public sealed class CreateRetentionRunRequest
{
    [Required]
    [RegularExpression("^(Candidate|Employment|Audit)$")]
    public string DataCategory { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int PolicyVersion { get; set; }

    [Range(1, 1000)]
    public int MaxRecords { get; set; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string AuthorizationReason { get; set; } = "";

    [Required]
    public bool? ConfirmAuthorization { get; set; }
}

public sealed class UpsertLegalHoldRequest
{
    [Required]
    [RegularExpression("^(All|Candidate|Employment|Audit)$")]
    public string Scope { get; set; } = "";

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; set; } = "";

    [StringLength(24)]
    public string? RowVersion { get; set; }
}

public sealed class ReleaseLegalHoldRequest
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; set; } = "";

    [Required, StringLength(24)]
    public string RowVersion { get; set; } = "";
}
