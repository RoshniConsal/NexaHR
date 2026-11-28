namespace NexaHR.Domain.Entities;

public sealed class CvMatchRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid VacancyRevisionId { get; set; }

    public Guid CvReviewVersionId { get; set; }

    public Guid SkillCatalogueVersionId { get; set; }

    public int Version { get; set; }

    public string RuleVersion { get; set; }
        = string.Empty;

    public string SourceSha256Hash { get; set; }
        = string.Empty;

    public string InputSha256Hash { get; set; }
        = string.Empty;

    public decimal MatchedWeight { get; set; }

    public decimal TotalWeight { get; set; }

    public decimal? Score { get; set; }

    public string CriteriaSnapshotJson { get; set; }
        = "[]";

    public string EvidenceJson { get; set; }
        = "[]";

    public string ExperienceAssessmentJson { get; set; }
        = "{}";

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = [];
}
