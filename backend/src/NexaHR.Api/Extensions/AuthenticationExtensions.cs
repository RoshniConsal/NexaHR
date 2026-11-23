using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NexaHR.Domain.Constants;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;
using System.Text;

namespace NexaHR.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddNexaHRAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ASP.NET Core Identity configuration
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<NexaHRDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        // JWT configuration
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var jwtAudience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal;

                    if (!Guid.TryParse(
                        principal?.FindFirst(
                            ClaimTypes.NameIdentifier)?.Value,
                        out var userId) ||
                        !Guid.TryParse(
                            (principal?.FindFirst("sid")?.Value ?? principal?.FindFirst(ClaimTypes.Sid)?.Value),
                            out var sessionId))
                    {
                        context.Fail("Invalid session.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices
                        .GetRequiredService<NexaHRDbContext>();

                    var now = DateTime.UtcNow;

                    var validSession = await db.AuthSessions
                        .AsNoTracking()
                        .AnyAsync(session =>
                            session.Id == sessionId &&
                            session.UserId == userId &&
                            session.RevokedAtUtc == null &&
                            session.ExpiresAtUtc > now);

                    if (!validSession)
                    {
                        context.Fail("Session expired or revoked.");
                        return;
                    }

                    var userManager = context.HttpContext
                        .RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    var user = await userManager.FindByIdAsync(
                        userId.ToString());

                    if (user is null ||
                        !user.IsActive ||
                        !user.EmailConfirmed ||
                        await userManager.IsLockedOutAsync(user))
                    {
                        context.Fail("Account unavailable.");
                        return;
                    }

                    var roles = await userManager.GetRolesAsync(user);

                    var tokenRoles = principal!
                        .FindAll(ClaimTypes.Role)
                        .Select(claim => claim.Value)
                        .ToArray();

                    if (roles.Count != 1 ||
                        !AppRoles.All.Contains(roles[0]) ||
                        tokenRoles.Length != 1 ||
                        tokenRoles[0] != roles[0])
                    {
                        context.Fail("Account role changed.");
                    }
                }
            };

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey)),

                    ClockSkew = TimeSpan.FromSeconds(30)
                };
        });

        services.AddAuthorization();

        return services;
    }
}
