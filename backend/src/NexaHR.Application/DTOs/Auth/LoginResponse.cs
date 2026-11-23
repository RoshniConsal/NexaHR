namespace NexaHR.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public Guid? UserId { get; init; }

    public string? FullName { get; init; }

    public string? Email { get; init; }

    public string[] Roles { get; init; } = [];

    public string? AccessToken { get; init; }

    public string? RefreshToken { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }
}
