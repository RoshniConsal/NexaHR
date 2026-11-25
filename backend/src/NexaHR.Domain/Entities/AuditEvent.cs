namespace NexaHR.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ActorUserId { get; set; }

    public string ActorRole { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string? ReasonCode { get; set; }

    public bool IsHrVisible { get; set; }

    public bool IsCeoVisible { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}
