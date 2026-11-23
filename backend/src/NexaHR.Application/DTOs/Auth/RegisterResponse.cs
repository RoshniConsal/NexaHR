namespace NexaHR.Application.DTOs.Auth;

public sealed class RegisterResponse
{
    public bool Success { get; init; }

    public Guid? UserId { get; init; }

    public string Message { get; init; } = string.Empty;

    public string[] Errors { get; init; } = [];
}
