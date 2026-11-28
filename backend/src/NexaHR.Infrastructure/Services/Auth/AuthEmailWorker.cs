using NexaHR.Infrastructure.Services.Employees;
using NexaHR.Infrastructure.Services.Invitations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexaHR.Infrastructure.Persistence;
using System.Net;
using System.Net.Mail;

namespace NexaHR.Infrastructure.Services.Auth;

public sealed class AuthEmailWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IDataProtector _protector;
    private readonly ILogger<AuthEmailWorker> _logger;

    public AuthEmailWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IDataProtectionProvider provider,
        ILogger<AuthEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        _protector = provider.CreateProtector(
            "NexaHR.AuthEmailOutbox.v1");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("Email:Enabled"))
        {
            _logger.LogInformation(
                "Authentication email worker disabled.");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                _logger.LogWarning(
                    "Email outbox processing failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(15),
                stoppingToken);
        }
    }

    private async Task ProcessEmailsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<NexaHRDbContext>();

        var now = DateTime.UtcNow;

        var messages = await db.AuthEmailOutbox
            .Where(e =>
                e.SentAtUtc == null &&
                e.ProtectedToken != string.Empty &&
                e.ExpiresAtUtc > now &&
                e.AttemptCount < 5 &&
                (e.NextAttemptAtUtc == null ||
                 e.NextAttemptAtUtc <= now))
            .OrderBy(e => e.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var entry in messages)
        {
            if (entry.ExpiresAtUtc <= DateTime.UtcNow)
                continue;

            try
            {
                var token = _protector.Unprotect(
                    entry.ProtectedToken);

                if (entry.EmailType == "Invitation")
                {
                    var tokenHash =
                        InvitationTokenService.HashToken(token);

                    var checkTime = DateTime.UtcNow;

                    var valid = await db.AccountInvitations
                        .AsNoTracking()
                        .AnyAsync(i =>
                            i.TokenHash == tokenHash &&
                            i.Email == entry.RecipientEmail &&
                            i.AcceptedAtUtc == null &&
                            i.RevokedAtUtc == null &&
                            i.ExpiresAtUtc > checkTime,
                            cancellationToken);

                    if (!valid)
                    {
                        entry.ProtectedToken = string.Empty;
                        entry.ExpiresAtUtc = checkTime;
                        entry.NextAttemptAtUtc = null;

                        await db.SaveChangesAsync(
                            cancellationToken);

                        continue;
                    }
                }

                if (entry.EmailType == "WorkEmailVerification")
                {
                    var checkTime = DateTime.UtcNow;

                    var valid = WorkEmailToken.TryHash(
                        token, out var tokenHash)
                        && await db.WorkEmailVerifications
                            .AsNoTracking()
                            .AnyAsync(v =>
                                v.TokenHash == tokenHash &&
                                v.ProposedEmail == entry.RecipientEmail &&
                                v.ConfirmedAtUtc == null &&
                                v.RevokedAtUtc == null &&
                                v.ExpiresAtUtc > checkTime,
                                cancellationToken);

                    if (!valid)
                    {
                        entry.ProtectedToken = string.Empty;
                        entry.ExpiresAtUtc = checkTime;
                        entry.NextAttemptAtUtc = null;

                        await db.SaveChangesAsync(
                            cancellationToken);

                        continue;
                    }
                }

                await SendEmailAsync(
                    entry.RecipientEmail,
                    entry.EmailType,
                    token,
                    cancellationToken);

                entry.SentAtUtc = DateTime.UtcNow;
                entry.ProtectedToken = string.Empty;
                entry.NextAttemptAtUtc = null;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                entry.AttemptCount++;

                entry.NextAttemptAtUtc =
                    DateTime.UtcNow.AddMinutes(
                        Math.Pow(2, entry.AttemptCount));

                _logger.LogWarning(
                    "Authentication email delivery failed. Attempt {Attempt}.",
                    entry.AttemptCount);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendEmailAsync(
        string email,
        string emailType,
        string token,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Email:SmtpHost"]
            ?? throw new InvalidOperationException(
                "SMTP host missing.");

        var sender = _configuration["Email:SenderEmail"]
            ?? throw new InvalidOperationException(
                "Sender email missing.");

        var username = _configuration["Email:Username"]
            ?? throw new InvalidOperationException(
                "SMTP username missing.");

        var password = _configuration["Email:Password"]
            ?? throw new InvalidOperationException(
                "SMTP password missing.");

        var frontendUrl =
            _configuration["Email:FrontendBaseUrl"]
            ?? throw new InvalidOperationException(
                "Frontend URL missing.");

        var (route, subject) = emailType switch
        {
            "VerifyEmail" => (
                "verify-email",
                "NexaHR - Verify your email"),

            "ResetPassword" => (
                "reset-password",
                "NexaHR - Reset your password"),

            "WorkEmailVerification" => (
                "verify-work-email",
                "NexaHR - Verify your work email"),

            "Invitation" => (
                "accept-invitation",
                "NexaHR - Account invitation"),

            _ => throw new InvalidOperationException(
                "Unsupported email type.")
        };

        var link = $"{frontendUrl.TrimEnd('/')}/{route}" +
            $"?email={Uri.EscapeDataString(email)}" +
            $"&token={Uri.EscapeDataString(token)}";

        using var message = new MailMessage(sender, email);

        message.Subject = subject;

        message.Body =
            "Hello,\n\n" +
            "Please use the following link:\n\n" +
            link +
            "\n\nIf you did not request this, ignore this email." +
            "\n\nNexaHR Team";

        using var client = new SmtpClient(
            host,
            _configuration.GetValue<int>("Email:SmtpPort", 587))
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                username, password)
        };

        await client.SendMailAsync(
            message, cancellationToken);
    }
}
