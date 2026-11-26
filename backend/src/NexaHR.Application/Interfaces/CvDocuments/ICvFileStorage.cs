namespace NexaHR.Application.Interfaces.CvDocuments;

public interface ICvFileStorage
{
    Task<CvStoredFile> SaveToQuarantineAsync(
        Stream content,
        CancellationToken cancellationToken = default);

    string GetQuarantinePath(string storageKey);

    void PromoteToClean(string storageKey);

    Stream OpenCleanRead(string storageKey);

    void Delete(string storageKey);
}

public sealed class CvStoredFile
{
    public string StorageKey { get; init; }
        = string.Empty;

    public long SizeBytes { get; init; }

    public string Sha256Hash { get; init; }
        = string.Empty;
}
