using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Auth;

public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(4096)]
    public string Token { get; set; } = string.Empty;
}
