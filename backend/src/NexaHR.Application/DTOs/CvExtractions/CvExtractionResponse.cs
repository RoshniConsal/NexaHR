using System.Text.Json;

namespace NexaHR.Application.DTOs.CvExtractions;

public sealed class CvExtractionResponse
{
    public Guid Id { get; init; }

    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    public int Version { get; init; }

    public string SourceSha256Hash { get; init; } = "";

    public string ParserVersion { get; init; } = "";

    public string Status { get; init; } = "";

    public JsonElement? Result { get; init; }

    public JsonElement? Evidence { get; init; }

    public string? FailureCode { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}

public sealed record CvExtractionOperationResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data)
    where T : class;
