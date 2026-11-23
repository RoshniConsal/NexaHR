namespace NexaHR.Domain.Entities;

public sealed class AccountInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public Guid IssuedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public Guid? AcceptedByUserId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public string? RevocationReason { get; set; }

    public Guid? ReissuedFromInvitationId { get; set; }
}
