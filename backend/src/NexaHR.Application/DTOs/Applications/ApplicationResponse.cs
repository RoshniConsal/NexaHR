namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationResponse
{
    public Guid ApplicationId { get; init; }

    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid DocumentId { get; init; }

    public string JobTitle { get; init; }
        = string.Empty;

    public string Stage { get; init; }
        = string.Empty;

    public DateTime AppliedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public string RowVersion { get; init; }
        = string.Empty;
}

public sealed class ApplicationOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationResponse? Application { get; init; }
}
