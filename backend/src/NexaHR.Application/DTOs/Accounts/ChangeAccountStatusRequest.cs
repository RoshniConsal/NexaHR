using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Accounts;

public sealed class ChangeAccountStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
