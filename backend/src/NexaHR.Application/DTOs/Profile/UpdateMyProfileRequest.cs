using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Profile;

public sealed class UpdateMyProfileRequest
{
    [StringLength(200, MinimumLength = 2)]
    public string? FullName { get; set; }

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}
