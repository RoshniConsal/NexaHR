namespace NexaHR.Application.DTOs.Accounts;

public sealed class AccountResponse
{
    public Guid AccountId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool HasPrivilegedAccess { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime? DeactivatedAtUtc { get; init; }
}
