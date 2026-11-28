using System.Security.Cryptography;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvReviews;

public sealed partial class CvReviewService
{
    private const long MaxReviewCvBytes =
        5L * 1024 * 1024;

    private async Task<bool> VerifyReviewCvIntegrityAsync(
        CvDocument document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document.ScanStatus != CvScanStatuses.Clean ||
            !document.ScannedAtUtc.HasValue ||
            document.SizeBytes <= 0 ||
            document.SizeBytes > MaxReviewCvBytes ||
            string.IsNullOrWhiteSpace(document.StorageKey) ||
            string.IsNullOrWhiteSpace(document.Sha256Hash) ||
            document.Sha256Hash.Length != 64 ||
            !document.Sha256Hash.All(Uri.IsHexDigit))
        {
            return false;
        }

        try
        {
            await using var content =
                _storage.OpenCleanRead(document.StorageKey);

            if (!content.CanRead ||
                !content.CanSeek ||
                content.Length != document.SizeBytes)
            {
                return false;
            }

            content.Position = 0;

            using var hasher = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

            var buffer = new byte[81920];

            long totalBytes = 0;

            while (true)
            {
                var bytesRead = await content.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;

                if (totalBytes > document.SizeBytes ||
                    totalBytes > MaxReviewCvBytes)
                {
                    return false;
                }

                hasher.AppendData(
                    buffer, 0, bytesRead);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (totalBytes != document.SizeBytes)
            {
                return false;
            }

            var actualHash = hasher.GetHashAndReset();

            var expectedHash = Convert.FromHexString(
                document.Sha256Hash);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (ArgumentException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (NotSupportedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (FormatException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }
}
