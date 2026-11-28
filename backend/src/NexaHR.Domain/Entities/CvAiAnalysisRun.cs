using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class CvAiAnalysisRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid VacancyRevisionId { get; set; }

    public Guid CvReviewVersionId { get; set; }

    public Guid CvMatchRunId { get; set; }

    public Guid ProcessingPolicyId { get; set; }

    public Guid ApplicationConsentId { get; set; }

    public int Version { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ModelDigest { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public int InputSchemaVersion { get; set; } = 1;

    public int OutputSchemaVersion { get; set; } = 1;

    public string SourceSha256Hash { get; set; } =
        string.Empty;

    public string InputSha256Hash { get; set; } =
        string.Empty;

    public string InputJson { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        CvAiAnalysisStatuses.Queued;

    public int AttemptCount { get; set; }

    public string? ResultJson { get; set; }

    public string? FailureCode { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
