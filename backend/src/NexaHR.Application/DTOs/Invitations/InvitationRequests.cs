using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Invitations;

public sealed class IssueInvitationRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Role { get; set; } = string.Empty;
}

public sealed class ReissueInvitationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class RevokeInvitationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class InvitationTokenRequest
{
    [Required]
    [StringLength(256)]
    public string Token { get; set; } = string.Empty;
}

public sealed class InvitationQuery
{
    [StringLength(256)]
    public string? Search { get; set; }

    [StringLength(30)]
    public string? Role { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class AcceptInvitationRequest
{
    [Required]
    [StringLength(256)]
    public string Token { get; set; } = string.Empty;

    [StringLength(200, MinimumLength = 2)]
    public string? FullName { get; set; }

    [StringLength(128, MinimumLength = 8)]
    public string? Password { get; set; }

    [Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }
}
