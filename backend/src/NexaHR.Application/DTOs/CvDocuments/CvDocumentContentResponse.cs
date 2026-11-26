namespace NexaHR.Application.DTOs.CvDocuments;

public sealed class CvDocumentContentResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public Stream? Content { get; init; }

    public string ContentType { get; init; }
        = string.Empty;

    public string FileName { get; init; }
        = string.Empty;
}
