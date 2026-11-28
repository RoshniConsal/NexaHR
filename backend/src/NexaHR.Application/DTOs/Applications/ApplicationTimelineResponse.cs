namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationTimelineEventResponse
{
    public Guid EventId { get; init; }

    public string? FromStage { get; init; }

    public string ToStage { get; init; }
        = string.Empty;

    public DateTime AtUtc { get; init; }

    // HR-only information.
    public Guid? ActorUserId { get; init; }

    public string? ActorRole { get; init; }

    public string? Reason { get; init; }
}

public sealed class ApplicationTimelineResponse
{
    public Guid ApplicationId { get; init; }

    public string CurrentStage { get; init; }
        = string.Empty;

    public IReadOnlyList<ApplicationTimelineEventResponse>
        Events { get; init; } = [];
}

public sealed class ApplicationTimelineOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationTimelineResponse? Data { get; init; }
}
