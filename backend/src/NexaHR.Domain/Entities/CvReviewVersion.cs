using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class CvReviewVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid VacancyRevisionId { get; set; }

    public Guid? ExtractionRunId { get; set; }

    public Guid ReviewerUserId { get; set; }

    public Guid? PreviousReviewVersionId { get; set; }

    public int Version { get; set; }

    public string Status { get; set; }
        = CvReviewStatuses.Draft;

    public string ValuesJson { get; set; }
        = "{}";

    public string DuplicateResolutionJson { get; set; }
        = "{}";

    public string ChangedFieldsJson { get; set; }
        = "[]";

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ConfirmedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
