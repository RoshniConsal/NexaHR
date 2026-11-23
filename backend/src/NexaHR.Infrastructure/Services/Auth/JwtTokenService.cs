using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Services.Auth;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAtUtc)
        GenerateAccessToken(
            ApplicationUser user,
            IEnumerable<string> roles,
            Guid sessionId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(ClaimTypes.Name, user.FullName),

            new(ClaimTypes.Email,
                user.Email ?? string.Empty),

            new(JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new("sid", sessionId.ToString())
        };

        claims.AddRange(roles.Select(
            role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT key is missing.")));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler()
                .WriteToken(token),
            expiresAt);
    }

    public static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }

    public static string HashRefreshToken(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }
}
