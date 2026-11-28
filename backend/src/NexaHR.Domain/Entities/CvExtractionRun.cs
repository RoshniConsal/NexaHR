using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class CvExtractionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public Guid DocumentId { get; set; }

    public int Version { get; set; }

    public string SourceSha256Hash { get; set; }
        = string.Empty;

    public string ParserVersion { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = CvExtractionStatuses.Queued;

    public string? ResultJson { get; set; }

    public string? EvidenceJson { get; set; }

    public string? FailureCode { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
