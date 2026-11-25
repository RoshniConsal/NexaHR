namespace NexaHR.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientUserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
