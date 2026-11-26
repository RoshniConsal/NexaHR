using System.Security.Cryptography;
using NexaHR.Application.Interfaces.CvDocuments;

namespace NexaHR.Infrastructure.Services.CvDocuments;

public sealed class CvFileStorage : ICvFileStorage
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private readonly string _quarantineDirectory;
    private readonly string _cleanDirectory;

    public CvFileStorage()
    {
        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException(
                "Private CV storage is unavailable.");
        }

        var root = Path.Combine(
            home, ".nexahr", "private-cv");

        _quarantineDirectory =
            Path.Combine(root, "quarantine");

        _cleanDirectory =
            Path.Combine(root, "clean");

        EnsurePrivateDirectory(root);
        EnsurePrivateDirectory(_quarantineDirectory);
        EnsurePrivateDirectory(_cleanDirectory);
    }

    public async Task<CvStoredFile> SaveToQuarantineAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageKey = Guid.NewGuid().ToString("N");

        var path = GetQuarantinePath(storageKey);

        long totalBytes = 0;
        var created = false;

        using var hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };

            if (OperatingSystem.IsLinux())
            {
                options.UnixCreateMode =
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite;
            }

            await using (var destination =
                new FileStream(path, options))
            {
                created = true;

                var buffer = new byte[81920];

                while (true)
                {
                    var read = await content.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken);

                    if (read == 0)
                        break;

                    totalBytes += read;

                    if (totalBytes > MaxFileSize)
                    {
                        throw new InvalidDataException(
                            "CV file exceeds 5 MB.");
                    }

                    hash.AppendData(buffer, 0, read);

                    await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }

                if (totalBytes == 0)
                {
                    throw new InvalidDataException(
                        "CV file is empty.");
                }

                await destination.FlushAsync(
                    cancellationToken);
            }

            return new CvStoredFile
            {
                StorageKey = storageKey,
                SizeBytes = totalBytes,
                Sha256Hash = Convert.ToHexString(
                    hash.GetHashAndReset())
            };
        }
        catch
        {
            if (created)
                File.Delete(path);

            throw;
        }
    }

    public string GetQuarantinePath(string storageKey)
    {
        ValidateStorageKey(storageKey);

        return Path.Combine(
            _quarantineDirectory, storageKey);
    }

    public void PromoteToClean(string storageKey)
    {
        var source = GetQuarantinePath(storageKey);

        var destination =
            GetCleanPath(storageKey);

        File.Move(
            source,
            destination,
            overwrite: false);
    }

    public Stream OpenCleanRead(string storageKey)
    {
        return new FileStream(
            GetCleanPath(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    public void Delete(string storageKey)
    {
        File.Delete(GetQuarantinePath(storageKey));
        File.Delete(GetCleanPath(storageKey));
    }

    private string GetCleanPath(string storageKey)
    {
        ValidateStorageKey(storageKey);

        return Path.Combine(
            _cleanDirectory, storageKey);
    }

    private static void ValidateStorageKey(
        string storageKey)
    {
        if (!Guid.TryParseExact(
                storageKey, "N", out var id) ||
            id == Guid.Empty)
        {
            throw new ArgumentException(
                "Invalid CV storage key.");
        }
    }

    private static void EnsurePrivateDirectory(
        string path)
    {
        if (OperatingSystem.IsLinux())
        {
            var mode =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute;

            Directory.CreateDirectory(path, mode);

            File.SetUnixFileMode(path, mode);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Private CV storage currently requires Linux.");
        }
    }
}
