namespace NexaHR.Application.DTOs.Jobs;

public sealed class PublicJobDetailsResponse
{
    public Guid JobId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string DepartmentName { get; init; } = string.Empty;

    public string EmploymentType { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string WorkMode { get; init; } = string.Empty;

    public int Openings { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal? MinimumExperienceYears { get; init; }

    public DateOnly ClosingDate { get; init; }

    public DateTime PublishedAtUtc { get; init; }

    public List<string> RequiredSkills { get; init; } = [];

    public List<string> PreferredSkills { get; init; } = [];
}
