using NexaHR.Application.DTOs.Profile;

namespace NexaHR.Application.Interfaces.Profile;

public interface IProfileService
{
    Task<MyProfileResponse?> GetMyProfileAsync(
        Guid userId);

    Task<UpdateMyProfileResponse> UpdateMyProfileAsync(
        Guid userId,
        UpdateMyProfileRequest request);
}
