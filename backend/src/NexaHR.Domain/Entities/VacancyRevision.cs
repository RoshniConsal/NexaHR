namespace NexaHR.Domain.Entities;

public sealed class VacancyRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VacancyId { get; set; }

    public int Revision { get; set; } = 1;

    public Guid DepartmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public int Openings { get; set; }

    public string Location { get; set; } = string.Empty;

    public string WorkMode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal? MinimumExperienceYears { get; set; }

    public DateOnly ClosingDate { get; set; }

    public string HiringReason { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAtUtc { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
