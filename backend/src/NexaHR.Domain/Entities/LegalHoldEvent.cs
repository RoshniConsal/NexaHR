namespace NexaHR.Domain.Entities;

public sealed class LegalHoldEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LegalHoldId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
