using Microsoft.EntityFrameworkCore;
using NexaHR.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using NexaHR.Application.DTOs.Auth;
using NexaHR.Application.Interfaces.Auth;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly NexaHRDbContext _dbContext;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAuthEmailService _authEmailService;


    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        NexaHRDbContext dbContext,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        IAuthEmailService authEmailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _authEmailService = authEmailService;

    }

    public async Task<RegisterResponse> RegisterAsync(
        RegisterRequest request)
    {
        var email = request.Email.Trim();

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Registration could not be completed."
            };
        }

        if (!await _roleManager.RoleExistsAsync(
            AppRoles.Candidate))
        {
            throw new InvalidOperationException(
                "Candidate role has not been configured.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            UserName = email,
            IsActive = true,
            EmailConfirmed = false,
            HasPrivilegedAccess = false,
            CreatedAtUtc = DateTime.UtcNow,
            LockoutEnabled = true
        };

        var createResult =
            await _userManager.CreateAsync(
                user, request.Password);

        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();

            return new RegisterResponse
            {
                Success = false,
                Message = "Registration could not be completed.",
                Errors = createResult.Errors
                    .Where(e => e.Code.StartsWith("Password"))
                    .Select(e => e.Description)
                    .ToArray()
            };
        }

        var roleResult =
            await _userManager.AddToRoleAsync(
                user, AppRoles.Candidate);

        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();

            return new RegisterResponse
            {
                Success = false,
                Message = "Registration could not be completed."
            };
        }


        var verificationToken =
            await _userManager
                .GenerateEmailConfirmationTokenAsync(user);

        await _authEmailService
            .QueueVerificationEmailAsync(
                email, verificationToken);

        await transaction.CommitAsync();

        return new RegisterResponse
        {
            Success = true,
            UserId = user.Id,
            Message = "Candidate account created successfully."
        };
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid credentials or account unavailable."
            };
        }

        var result =
            await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid credentials or account unavailable."
            };
        }

        if (!user.EmailConfirmed)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Please verify your email before signing in."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0]))
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Account role is not configured correctly."
            };
        }

        var refreshToken =
            JwtTokenService.GenerateRefreshToken();

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash =
                JwtTokenService.HashRefreshToken(refreshToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        var accessToken =
            _jwtTokenService.GenerateAccessToken(
                user, roles, session.Id);

        _dbContext.AuthSessions.Add(session);

        await _dbContext.SaveChangesAsync();

        return new LoginResponse
        {
            Success = true,
            Message = "Login successful.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles.ToArray(),
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken,
            ExpiresAtUtc = accessToken.ExpiresAtUtc
        };
    }


    public async Task<bool> LogoutAsync(
        Guid userId,
        Guid sessionId)
    {
        var now = DateTime.UtcNow;

        var affected = await _dbContext.AuthSessions
            .Where(session =>
                session.Id == sessionId &&
                session.UserId == userId &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    session => session.RevokedAtUtc,
                    (DateTime?)now));

        return affected > 0;
    }

    public async Task<LoginResponse> RefreshAsync(
        RefreshTokenRequest request)
    {
        var tokenHash =
            JwtTokenService.HashRefreshToken(
                request.RefreshToken);

        var now = DateTime.UtcNow;

        var session = await _dbContext.AuthSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s =>
                s.RefreshTokenHash == tokenHash &&
                s.RevokedAtUtc == null &&
                s.ExpiresAtUtc > now);

        if (session is null)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid or expired session."
            };
        }

        var user = await _userManager.FindByIdAsync(
            session.UserId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _userManager.IsLockedOutAsync(user))
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Account unavailable."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0]))
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Account role unavailable."
            };
        }

        var newRefreshToken =
            JwtTokenService.GenerateRefreshToken();

        var newHash =
            JwtTokenService.HashRefreshToken(
                newRefreshToken);

        var accessToken =
            _jwtTokenService.GenerateAccessToken(
                user, roles, session.Id);

        var affected = await _dbContext.AuthSessions
            .Where(s =>
                s.Id == session.Id &&
                s.RefreshTokenHash == tokenHash &&
                s.RevokedAtUtc == null &&
                s.ExpiresAtUtc > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    s => s.RefreshTokenHash,
                    newHash)
                .SetProperty(
                    s => s.LastRefreshedAtUtc,
                    (DateTime?)now));

        if (affected != 1)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Session is no longer valid."
            };
        }

        return new LoginResponse
        {
            Success = true,
            Message = "Session refreshed successfully.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles.ToArray(),
            AccessToken = accessToken.Token,
            RefreshToken = newRefreshToken,
            ExpiresAtUtc = accessToken.ExpiresAtUtc
        };
    }


    // API-005: Verify email
    public async Task<AuthOperationResponse> VerifyEmailAsync(
        VerifyEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return new AuthOperationResponse
            {
                Success = false,
                Message = "Invalid verification request."
            };
        }

        var result = await _userManager.ConfirmEmailAsync(
            user, request.Token);

        return new AuthOperationResponse
        {
            Success = result.Succeeded,
            Message = result.Succeeded
                ? "Email verified successfully."
                : "Invalid or expired verification token."
        };
    }

    // API-006: Resend verification
    public async Task<AuthOperationResponse>
        ResendVerificationAsync(EmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is not null &&
            user.IsActive &&
            !user.EmailConfirmed &&
            user.Email is not null)
        {
            var token = await _userManager
                .GenerateEmailConfirmationTokenAsync(user);

            await _authEmailService
                .QueueVerificationEmailAsync(
                    user.Email, token);
        }

        return new AuthOperationResponse
        {
            Success = true,
            Message = "If eligible, a verification email will be sent."
        };
    }

    // API-007: Forgot password
    public async Task<AuthOperationResponse> ForgotPasswordAsync(
        EmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is not null &&
            user.IsActive &&
            user.EmailConfirmed &&
            user.Email is not null)
        {
            var token = await _userManager
                .GeneratePasswordResetTokenAsync(user);

            await _authEmailService
                .QueuePasswordResetEmailAsync(
                    user.Email, token);
        }

        return new AuthOperationResponse
        {
            Success = true,
            Message = "If eligible, a recovery email will be sent."
        };
    }

    // API-008: Reset password and revoke sessions
    public async Task<AuthOperationResponse> ResetPasswordAsync(
        ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed)
        {
            return new AuthOperationResponse
            {
                Success = false,
                Message = "Invalid password reset request."
            };
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync();

        var result = await _userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();

            return new AuthOperationResponse
            {
                Success = false,
                Message = "Password reset failed.",
                Errors = result.Errors
                    .Where(e => e.Code.StartsWith("Password"))
                    .Select(e => e.Description)
                    .ToArray()
            };
        }

        var now = DateTime.UtcNow;

        await _dbContext.AuthSessions
            .Where(s =>
                s.UserId == user.Id &&
                s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    s => s.RevokedAtUtc,
                    (DateTime?)now));

        await transaction.CommitAsync();

        return new AuthOperationResponse
        {
            Success = true,
            Message = "Password reset successfully."
        };
    }

}
