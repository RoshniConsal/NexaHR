namespace NexaHR.Domain.Entities;

public sealed class ApplicationStageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid ActorUserId { get; set; }

    public string ActorRole { get; set; }
        = string.Empty;

    public string? FromStage { get; set; }

    public string ToStage { get; set; }
        = string.Empty;

    public string? Reason { get; set; }

    public DateTime AtUtc { get; set; }
        = DateTime.UtcNow;
}
