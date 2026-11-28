namespace NexaHR.Domain.Entities;

public sealed class WorkEmailVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmployeeId { get; set; }

    public string ProposedEmail { get; set; } = string.Empty;

    public string NormalizedProposedEmail { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
