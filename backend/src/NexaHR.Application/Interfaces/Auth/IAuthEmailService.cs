namespace NexaHR.Application.Interfaces.Auth;

public interface IAuthEmailService
{
    Task QueueVerificationEmailAsync(
        string email,
        string token);

    Task QueuePasswordResetEmailAsync(
        string email,
        string token);

    Task QueueInvitationEmailAsync(
        string email,
        string token,
        DateTime expiresAtUtc);
}
