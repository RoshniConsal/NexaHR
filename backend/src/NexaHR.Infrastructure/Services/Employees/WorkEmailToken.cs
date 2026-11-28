using System.Security.Cryptography;

namespace NexaHR.Infrastructure.Services.Employees;

internal static class WorkEmailToken
{
    private const int TokenBytes = 32;

    internal static string Generate()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(TokenBytes));
    }

    internal static bool TryHash(
        string? token,
        out string hash)
    {
        hash = string.Empty;

        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != TokenBytes * 2)
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromHexString(token);

            if (bytes.Length != TokenBytes)
                return false;

            hash = Convert.ToHexString(
                SHA256.HashData(bytes));

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string Hash(string token)
    {
        if (!TryHash(token, out var hash))
            throw new ArgumentException(
                "Invalid verification token.",
                nameof(token));

        return hash;
    }
}
