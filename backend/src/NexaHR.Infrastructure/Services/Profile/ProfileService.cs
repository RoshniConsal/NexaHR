using Microsoft.AspNetCore.Identity;
using NexaHR.Application.DTOs.Profile;
using NexaHR.Application.Interfaces.Profile;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Services.Profile;

public sealed class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileService(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<MyProfileResponse?> GetMyProfileAsync(
        Guid userId)
    {
        var user = await _userManager.FindByIdAsync(
            userId.ToString());

        if (user is null || !user.IsActive ||
            !user.EmailConfirmed)
            return null;

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0]))
            return null;

        return MapProfile(user, roles[0]);
    }

    public async Task<UpdateMyProfileResponse>
        UpdateMyProfileAsync(
            Guid userId,
            UpdateMyProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(
            userId.ToString());

        if (user is null || !user.IsActive ||
            !user.EmailConfirmed)
        {
            return new UpdateMyProfileResponse
            {
                Success = false,
                Message = "Account unavailable."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0]))
        {
            return new UpdateMyProfileResponse
            {
                Success = false,
                Message = "Account role unavailable."
            };
        }

        if (request.FullName is null &&
            request.PhoneNumber is null)
        {
            return new UpdateMyProfileResponse
            {
                Success = false,
                Message = "No profile fields supplied."
            };
        }

        if (request.FullName is not null)
        {
            var name = request.FullName.Trim();

            if (name.Length < 2 || name.Length > 200)
            {
                return new UpdateMyProfileResponse
                {
                    Success = false,
                    Message = "Full name must contain 2 to 200 characters."
                };
            }

            user.FullName = name;
        }

        if (request.PhoneNumber is not null)
        {
            var phone = request.PhoneNumber.Trim();

            if (user.PhoneNumber != phone)
            {
                user.PhoneNumber = phone;
                user.PhoneNumberConfirmed = false;
            }
        }

        user.UpdatedAtUtc = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return new UpdateMyProfileResponse
            {
                Success = false,
                Message = "Profile update failed.",
                Errors = result.Errors
                    .Select(e => e.Description)
                    .ToArray()
            };
        }

        return new UpdateMyProfileResponse
        {
            Success = true,
            Message = "Profile updated successfully.",
            Profile = MapProfile(user, roles[0])
        };
    }

    private static MyProfileResponse MapProfile(
        ApplicationUser user,
        string role)
    {
        return new MyProfileResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = role,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc
        };
    }
}
