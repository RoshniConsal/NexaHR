namespace NexaHR.Domain.Entities;

public sealed class PolicyAcknowledgement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PolicyVersionId { get; set; }

    public Guid UserId { get; set; }

    public DateTime AcknowledgedAtUtc { get; set; } = DateTime.UtcNow;
}
