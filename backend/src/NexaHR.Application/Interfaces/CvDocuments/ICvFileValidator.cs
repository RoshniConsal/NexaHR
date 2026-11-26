namespace NexaHR.Application.Interfaces.CvDocuments;

public interface ICvFileValidator
{
    CvFileValidationResult Validate(
        string filePath,
        string originalFileName);
}

public sealed class CvFileValidationResult
{
    public bool IsValid { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string ContentType { get; init; }
        = string.Empty;

    public string SafeFileName { get; init; }
        = string.Empty;
}
