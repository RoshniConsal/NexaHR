using System.Text.Json;

namespace NexaHR.Application.DTOs.CvMatching;

public sealed class CvMatchResponse
{
    public Guid Id { get; init; }

    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid CvReviewVersionId { get; init; }

    public Guid SkillCatalogueVersionId { get; init; }

    public int Version { get; init; }

    public string RuleVersion { get; init; } = "";

    public string SourceSha256Hash { get; init; } = "";

    public string InputSha256Hash { get; init; } = "";

    public decimal MatchedWeight { get; init; }

    public decimal TotalWeight { get; init; }

    public decimal? Score { get; init; }

    public string ScoreStatus { get; init; } = "";

    public bool IsStale { get; init; }

    public IReadOnlyList<string> StaleReasons { get; init; }
        = [];

    public JsonElement? Criteria { get; init; }

    public JsonElement? Evidence { get; init; }

    public JsonElement? ExperienceAssessment { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed record CvMatchingOperationResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data)
    where T : class;
