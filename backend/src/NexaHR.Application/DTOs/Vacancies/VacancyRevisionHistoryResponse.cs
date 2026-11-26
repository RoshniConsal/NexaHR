namespace NexaHR.Application.DTOs.Vacancies;

public sealed class VacancyRevisionHistoryResponse
{
    public Guid VacancyId { get; init; }

    public string VacancyStatus { get; init; } = string.Empty;

    public Guid CurrentRevisionId { get; init; }

    public int CurrentRevision { get; init; }

    public Guid? PublishedRevisionId { get; init; }

    public int TotalRevisions { get; init; }

    public IReadOnlyList<VacancyRevisionHistoryItemResponse> Revisions
        { get; init; } = [];
}

public sealed class VacancyRevisionHistoryItemResponse
{
    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public bool IsCurrent { get; init; }

    public bool IsPublishedRevision { get; init; }

    public bool IsSubmitted { get; init; }

    public Guid DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string EmploymentType { get; init; } = string.Empty;

    public int Openings { get; init; }

    public string Location { get; init; } = string.Empty;

    public string WorkMode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal? MinimumExperienceYears { get; init; }

    public DateOnly ClosingDate { get; init; }

    public string HiringReason { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredSkills
        { get; init; } = [];

    public IReadOnlyList<string> PreferredSkills
        { get; init; } = [];

    public IReadOnlyList<VacancyCriterionResponse> Criteria
        { get; init; } = [];

    public Guid CreatedByUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid? SubmittedByUserId { get; init; }

    public DateTime? SubmittedAtUtc { get; init; }

    public VacancyDecisionResponse? Decision { get; init; }

    public string RowVersion { get; init; } = string.Empty;
}
