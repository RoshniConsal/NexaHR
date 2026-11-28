namespace NexaHR.Infrastructure.Services.Applications;

internal static class ContactNormalizer
{
    internal static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(
            value.Where(c => c >= '0' && c <= '9')
                .ToArray());

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length == 10 &&
            digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = "94" + digits[1..];
        }

        return digits.Length is >= 7 and <= 15
            ? digits
            : null;
    }
}
