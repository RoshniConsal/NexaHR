using System.Data;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Invitations;
using NexaHR.Application.Interfaces.Invitations;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Services.Invitations;

public sealed partial class InvitationService
    : IInvitationService
{
    private static string MaskEmail(string email)
    {
        var separator = email.LastIndexOf('@');

        if (separator <= 0)
            return "***";

        return email[0] + "***" +
            email.Substring(separator);
    }

    // API-018
    public async Task<InvitationPreviewResponse>
        PreviewAsync(InvitationTokenRequest request)
    {
        var hash =
            InvitationTokenService.HashToken(request.Token);

        var now = DateTime.UtcNow;

        var invitation = await _db.AccountInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(i =>
                i.TokenHash == hash &&
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc == null &&
                i.ExpiresAtUtc > now);

        if (invitation is null ||
            !AppRoles.All.Contains(invitation.Role))
        {
            return new InvitationPreviewResponse
            {
                IsValid = false,
                Message = "Invitation is invalid or unavailable."
            };
        }

        return new InvitationPreviewResponse
        {
            IsValid = true,
            Message = "Invitation is valid.",
            MaskedEmail = MaskEmail(invitation.Email),
            Role = invitation.Role,
            ExpiresAtUtc = invitation.ExpiresAtUtc
        };
    }

    // API-019
    public async Task<InvitationAcceptanceResponse>
        AcceptAsync(
            Guid? userId,
            AcceptInvitationRequest request)
    {
        var hash =
            InvitationTokenService.HashToken(request.Token);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var invitation = await _db.AccountInvitations
            .SingleOrDefaultAsync(i =>
                i.TokenHash == hash);

        if (invitation is null ||
            !AppRoles.All.Contains(invitation.Role))
        {
            return AcceptFail(
                "invalid_invitation",
                "Invitation is unavailable.");
        }

        ApplicationUser? user = null;

        if (userId.HasValue)
        {
            user = await _users.FindByIdAsync(
                userId.Value.ToString());

            if (user is null ||
                !user.IsActive ||
                !user.EmailConfirmed ||
                await _users.IsLockedOutAsync(user) ||
                user.NormalizedEmail !=
                    invitation.NormalizedEmail)
            {
                return AcceptFail(
                    "identity_mismatch",
                    "Unable to accept this invitation.");
            }
        }

        // Safe retry by the originally accepted identity.
        if (invitation.AcceptedAtUtc.HasValue)
        {
            if (user is not null &&
                invitation.AcceptedByUserId == user.Id)
            {
                var currentRoles =
                    await _users.GetRolesAsync(user);

                if (currentRoles.Count == 1 &&
                    currentRoles[0] == invitation.Role)
                {
                    await transaction.CommitAsync();

                    return Accepted(invitation, user.Id);
                }
            }

            return AcceptFail(
                "invalid_invitation",
                "Invitation is unavailable.");
        }

        var now = DateTime.UtcNow;

        if (invitation.RevokedAtUtc.HasValue ||
            invitation.ExpiresAtUtc <= now)
        {
            return AcceptFail(
                "invalid_invitation",
                "Invitation is unavailable.");
        }

        // Existing verified identity.
        if (user is not null)
        {
            var roles = await _users.GetRolesAsync(user);

            if (roles.Count != 1 ||
                roles[0] != invitation.Role)
            {
                return AcceptFail(
                    "role_conflict",
                    "Invitation cannot change your existing role.");
            }
        }
        else
        {
            // Never overwrite an existing identity.
            var existing = await _users.FindByEmailAsync(
                invitation.Email);

            if (existing is not null)
            {
                return AcceptFail(
                    "sign_in_required",
                    "Sign in with the intended verified account or use account recovery.");
            }

            var fullName = request.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(fullName) ||
                fullName.Length < 2 ||
                fullName.Length > 200 ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password != request.ConfirmPassword)
            {
                return AcceptFail(
                    "credentials_required",
                    "Valid name and matching passwords are required.");
            }

            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = invitation.Email,
                UserName = invitation.Email,
                EmailConfirmed = true,
                IsActive = true,
                HasPrivilegedAccess = false,
                LockoutEnabled = true,
                CreatedAtUtc = now
            };

            var createResult = await _users.CreateAsync(
                user, request.Password);

            if (!createResult.Succeeded)
            {
                return AcceptFail(
                    "creation_failed",
                    "Account setup failed. Check the password requirements.");
            }

            var roleResult = await _users.AddToRoleAsync(
                user, invitation.Role);

            if (!roleResult.Succeeded)
            {
                return AcceptFail(
                    "role_assignment_failed",
                    "Account setup could not be completed.");
            }
        }

        invitation.AcceptedAtUtc = DateTime.UtcNow;
        invitation.AcceptedByUserId = user.Id;

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return Accepted(invitation, user.Id);
    }

    private static InvitationAcceptanceResponse Accepted(
        AccountInvitation invitation,
        Guid userId)
    {
        return new InvitationAcceptanceResponse
        {
            Success = true,
            Code = "success",
            Message = "Invitation accepted successfully.",
            UserId = userId,
            InvitationId = invitation.Id,
            Role = invitation.Role,
            AcceptedAtUtc = invitation.AcceptedAtUtc
        };
    }

    private static InvitationAcceptanceResponse AcceptFail(
        string code,
        string message)
    {
        return new InvitationAcceptanceResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }
}
