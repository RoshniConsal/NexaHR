namespace NexaHR.Application.DTOs.CvDocuments;

public sealed class CvDocumentResponse
{
    public Guid DocumentId { get; init; }

    public int Version { get; init; }

    public string OriginalFileName { get; init; }
        = string.Empty;

    public string ContentType { get; init; }
        = string.Empty;

    public long SizeBytes { get; init; }

    public string ScanStatus { get; init; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ScannedAtUtc { get; init; }

    public bool CanDownload { get; init; }
}

public sealed class CvDocumentOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public CvDocumentResponse? Document { get; init; }
}
