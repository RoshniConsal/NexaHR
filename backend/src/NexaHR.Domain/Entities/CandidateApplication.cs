using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class CandidateApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidateUserId { get; set; }

    public Guid VacancyId { get; set; }

    public Guid VacancyRevisionId { get; set; }

    public Guid DocumentId { get; set; }

    public string Stage { get; set; }
        = ApplicationStatuses.Applied;

    public bool DuplicateEmailFlag { get; set; }

    public bool DuplicatePhoneFlag { get; set; }

    public DateTime AppliedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
