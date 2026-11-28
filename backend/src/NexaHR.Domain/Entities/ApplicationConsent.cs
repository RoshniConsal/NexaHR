namespace NexaHR.Domain.Entities;

public sealed class ApplicationConsent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid ActorUserId { get; set; }

    public string Purpose { get; set; }
        = string.Empty;

    public string? Provider { get; set; }

    public string TextVersion { get; set; }
        = string.Empty;

    public string ConsentText { get; set; }
        = string.Empty;

    public Guid? ProcessingPolicyId { get; set; }

    public bool Granted { get; set; }

    public DateTime AtUtc { get; set; }
        = DateTime.UtcNow;
}
