namespace NexaHR.Domain.Entities;

public sealed class CandidateProfile
{
    public Guid UserId { get; set; }

    public string? Skills { get; set; }

    public string? Education { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
