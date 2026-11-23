using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Auth;

public sealed class EmailRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
