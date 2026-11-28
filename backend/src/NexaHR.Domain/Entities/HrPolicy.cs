namespace NexaHR.Domain.Entities;

public sealed class HrPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? DraftTitle { get; set; }

    public string? DraftContent { get; set; }

    public bool DraftRequiresAcknowledgement { get; set; } = true;

    public bool DraftVisibleToEmployees { get; set; } = true;

    public bool DraftVisibleToHr { get; set; } = true;

    public bool DraftVisibleToCeo { get; set; }

    public bool HasDraft { get; set; } = true;

    public int? CurrentPublishedVersion { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
