using NexaHR.Application.DTOs.Auth;

namespace NexaHR.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(
        RegisterRequest request);

    Task<LoginResponse> LoginAsync(
        LoginRequest request);

    Task<bool> LogoutAsync(
        Guid userId,
        Guid sessionId);

    Task<LoginResponse> RefreshAsync(
        RefreshTokenRequest request);

    Task<AuthOperationResponse> VerifyEmailAsync(
        VerifyEmailRequest request);

    Task<AuthOperationResponse> ResendVerificationAsync(
        EmailRequest request);

    Task<AuthOperationResponse> ForgotPasswordAsync(
        EmailRequest request);

    Task<AuthOperationResponse> ResetPasswordAsync(
        ResetPasswordRequest request);
}
