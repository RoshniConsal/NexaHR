namespace NexaHR.Domain.Entities;

public sealed class HrPolicyVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PolicyId { get; set; }

    public int Version { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool RequiresAcknowledgement { get; set; }

    public bool VisibleToEmployees { get; set; }

    public bool VisibleToHr { get; set; }

    public bool VisibleToCeo { get; set; }

    public Guid PublishedByUserId { get; set; }

    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}
