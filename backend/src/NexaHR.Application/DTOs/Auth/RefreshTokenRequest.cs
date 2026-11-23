using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Auth;

public sealed class RefreshTokenRequest
{
    [Required]
    [StringLength(256)]
    public string RefreshToken { get; set; } = string.Empty;
}
