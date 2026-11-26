using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class CvDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerUserId { get; set; }

    public int Version { get; set; }

    public string OriginalFileName { get; set; }
        = string.Empty;

    public string StorageKey { get; set; }
        = string.Empty;

    public string ContentType { get; set; }
        = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256Hash { get; set; }
        = string.Empty;

    public string ScanStatus { get; set; }
        = CvScanStatuses.Pending;

    public string? ScanResultCode { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ScannedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
