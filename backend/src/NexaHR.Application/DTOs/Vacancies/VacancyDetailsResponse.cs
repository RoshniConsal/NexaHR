namespace NexaHR.Application.DTOs.Vacancies;

public sealed class VacancyDetailsResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public Guid DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string EmploymentType { get; init; } = string.Empty;

    public int Openings { get; init; }

    public string Location { get; init; } = string.Empty;

    public string WorkMode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal? MinimumExperienceYears { get; init; }

    public DateOnly ClosingDate { get; init; }

    public string HiringReason { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredSkills { get; init; } = [];

    public IReadOnlyList<string> PreferredSkills { get; init; } = [];

    public IReadOnlyList<VacancyCriterionResponse> Criteria { get; init; } = [];

    public Guid CreatedByUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime RevisionCreatedAtUtc { get; init; }

    public Guid? SubmittedByUserId { get; init; }

    public DateTime? SubmittedAtUtc { get; init; }

    public VacancyDecisionResponse? Decision { get; init; }

    public Guid? PublishedRevisionId { get; init; }

    public Guid? PublishedByUserId { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    public Guid? ClosedByUserId { get; init; }

    public DateTime? ClosedAtUtc { get; init; }

    public string? ClosureReason { get; init; }

    public string RowVersion { get; init; } = string.Empty;

    public string RevisionRowVersion { get; init; } = string.Empty;
}

public sealed class VacancyCriterionResponse
{
    public Guid CriterionId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public decimal Weight { get; init; }
}

public sealed class VacancyDecisionResponse
{
    public Guid DecisionId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid ApproverUserId { get; init; }

    public string ApproverRole { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public DateTime AtUtc { get; init; }
}
