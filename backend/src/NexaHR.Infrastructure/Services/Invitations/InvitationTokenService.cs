using System.Security.Cryptography;
using System.Text;

namespace NexaHR.Infrastructure.Services.Invitations;

public static class InvitationTokenService
{
    public static string GenerateToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);

        return Convert.ToHexString(
            SHA256.HashData(bytes));
    }
}
