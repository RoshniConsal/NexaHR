namespace NexaHR.Domain.Entities;

public sealed class ProcessingPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Version { get; set; }

    public int CandidateRetentionDays { get; set; }

    public int EmploymentRetentionDays { get; set; }

    public int AuditRetentionDays { get; set; }

    public string CandidateRetentionAction { get; set; } = string.Empty;

    public string EmploymentRetentionAction { get; set; } = string.Empty;

    public string AuditRetentionAction { get; set; } = string.Empty;

    public string ProcessingPurpose { get; set; } = string.Empty;

    public string DataCategories { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderRetention { get; set; } = string.Empty;

    public bool ProviderTrainingAllowed { get; set; }

    public string ProviderAccessPolicy { get; set; } = string.Empty;

    public string ProcessingLocation { get; set; } = string.Empty;

    public string LegalHoldPolicy { get; set; } = string.Empty;

    public string ApprovalReference { get; set; } = string.Empty;

    public Guid ApprovedByUserId { get; set; }

    public DateTime ApprovedAtUtc { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
