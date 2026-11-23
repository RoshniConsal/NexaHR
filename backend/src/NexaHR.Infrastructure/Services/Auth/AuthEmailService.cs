using Microsoft.AspNetCore.DataProtection;
using NexaHR.Application.Interfaces.Auth;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Auth;

public sealed class AuthEmailService : IAuthEmailService
{
    private readonly NexaHRDbContext _dbContext;
    private readonly IDataProtector _protector;

    public AuthEmailService(
        NexaHRDbContext dbContext,
        IDataProtectionProvider provider)
    {
        _dbContext = dbContext;

        _protector = provider.CreateProtector(
            "NexaHR.AuthEmailOutbox.v1");
    }

    public Task QueueVerificationEmailAsync(
        string email,
        string token)
    {
        return QueueAsync(
            email,
            token,
            "VerifyEmail");
    }

    public Task QueuePasswordResetEmailAsync(
        string email,
        string token)
    {
        return QueueAsync(
            email,
            token,
            "ResetPassword");
    }

    public Task QueueInvitationEmailAsync(
        string email,
        string token,
        DateTime expiresAtUtc)
    {
        return QueueAsync(
            email,
            token,
            "Invitation",
            expiresAtUtc);
    }

    private async Task QueueAsync(
        string email,
        string token,
        string emailType,
        DateTime? expiresAtUtc = null)
    {
        var now = DateTime.UtcNow;

        var message = new AuthEmailOutbox
        {
            Id = Guid.NewGuid(),
            RecipientEmail = email,
            EmailType = emailType,
            ProtectedToken = _protector.Protect(token),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            ExpiresAtUtc = expiresAtUtc ?? now.AddHours(1),
            AttemptCount = 0
        };

        _dbContext.Set<AuthEmailOutbox>().Add(message);

        await _dbContext.SaveChangesAsync();
    }
}
