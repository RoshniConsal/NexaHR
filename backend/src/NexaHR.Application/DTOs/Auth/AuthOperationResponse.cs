namespace NexaHR.Application.DTOs.Auth;

public sealed class AuthOperationResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string[] Errors { get; init; } = [];
}
