namespace NexaHR.Domain.Entities;

public sealed class LegalHold
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubjectId { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? ReleasedByUserId { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
