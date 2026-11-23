namespace NexaHR.Application.DTOs.Profile;

public sealed class UpdateMyProfileResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public MyProfileResponse? Profile { get; init; }

    public string[] Errors { get; init; } = [];
}
