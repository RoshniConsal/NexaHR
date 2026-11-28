using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Rules.Applications;

public static class ApplicationWithdrawalRules
{
    public const int MaximumReasonLength = 2000;

    public static bool IsEligibleStage(string? stage)
    {
        return stage is
            ApplicationStatuses.Applied or
            ApplicationStatuses.UnderReview or
            ApplicationStatuses.Shortlisted or
            ApplicationStatuses.Interview or
            ApplicationStatuses.OfferSent or
            ApplicationStatuses.OfferAccepted;
    }

    public static bool RequiresCancellationIntegration(
        string? stage)
    {
        return stage is
            ApplicationStatuses.Interview or
            ApplicationStatuses.OfferSent or
            ApplicationStatuses.OfferAccepted;
    }

    public static bool IsValidReason(string? reason)
    {
        return !string.IsNullOrWhiteSpace(reason) &&
               reason.Length <= MaximumReasonLength &&
               !reason.Any(char.IsControl);
    }

    public static bool TryReadRowVersion(
        string? value,
        out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();

        if (value is null || value.Length != 12)
            return false;

        var buffer = new byte[8];

        if (!Convert.TryFromBase64String(
                value,
                buffer,
                out var bytesWritten) ||
            bytesWritten != 8 ||
            Convert.ToBase64String(buffer) != value)
        {
            return false;
        }

        rowVersion = buffer;
        return true;
    }
}
