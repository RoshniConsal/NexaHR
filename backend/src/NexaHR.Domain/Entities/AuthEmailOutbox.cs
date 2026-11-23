namespace NexaHR.Domain.Entities;

public sealed class AuthEmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RecipientEmail { get; set; } = string.Empty;

    public string EmailType { get; set; } = string.Empty;

    public string ProtectedToken { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SentAtUtc { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public int AttemptCount { get; set; }
}
