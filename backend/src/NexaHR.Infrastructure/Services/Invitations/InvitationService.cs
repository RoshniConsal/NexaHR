using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Invitations;
using NexaHR.Application.Interfaces.Auth;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Invitations;

public sealed partial class InvitationService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuthEmailService _email;
    private readonly IAuditWriter _audit;

    public InvitationService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuthEmailService email,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _email = email;
        _audit = audit;
    }

    private async Task<ApplicationUser?> GetActorAsync(
        Guid actorId)
    {
        var actor = await _users.FindByIdAsync(
            actorId.ToString());

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
            return null;

        var roles = await _users.GetRolesAsync(actor);

        return roles.Count == 1 &&
            roles[0] == AppRoles.HRManager
            ? actor
            : null;
    }

    private static bool CanManageRole(
        ApplicationUser actor,
        string role)
    {
        return role == AppRoles.Candidate ||
               role == AppRoles.Employee ||
               (actor.HasPrivilegedAccess &&
                (role == AppRoles.CEO ||
                 role == AppRoles.HRManager));
    }

    private IQueryable<AccountInvitation> ManagedInvitations(
        ApplicationUser actor)
    {
        return _db.AccountInvitations.Where(i =>
            i.Role == AppRoles.Candidate ||
            i.Role == AppRoles.Employee ||
            (actor.HasPrivilegedAccess &&
             (i.Role == AppRoles.CEO ||
              i.Role == AppRoles.HRManager)));
    }

    private static string GetStatus(
        AccountInvitation invitation,
        DateTime now)
    {
        if (invitation.AcceptedAtUtc.HasValue)
            return "Accepted";

        if (invitation.RevokedAtUtc.HasValue)
            return "Revoked";

        return invitation.ExpiresAtUtc <= now
            ? "Expired"
            : "Pending";
    }

    private static InvitationResponse Map(
        AccountInvitation invitation,
        DateTime now)
    {
        return new InvitationResponse
        {
            InvitationId = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            Status = GetStatus(invitation, now),
            IssuedByUserId = invitation.IssuedByUserId,
            CreatedAtUtc = invitation.CreatedAtUtc,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            AcceptedAtUtc = invitation.AcceptedAtUtc,
            AcceptedByUserId = invitation.AcceptedByUserId,
            RevokedAtUtc = invitation.RevokedAtUtc,
            RevokedByUserId = invitation.RevokedByUserId,
            RevocationReason = invitation.RevocationReason,
            ReissuedFromInvitationId =
                invitation.ReissuedFromInvitationId
        };
    }

    private static InvitationOperationResponse Fail(
        string code,
        string message)
    {
        return new InvitationOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static bool ValidReason(string? reason)
    {
        return !string.IsNullOrWhiteSpace(reason) &&
            reason.Trim().Length >= 3 &&
            reason.Length <= 500;
    }

    // API-014
    public async Task<InvitationListResponse?>
        GetInvitationsAsync(
            Guid actorId,
            InvitationQuery query)
    {
        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return null;

        var now = DateTime.UtcNow;

        var items = ManagedInvitations(actor)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            items = items.Where(i =>
                i.Email.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            items = items.Where(i =>
                i.Role == query.Role);
        }

        items = query.Status switch
        {
            "Accepted" => items.Where(i =>
                i.AcceptedAtUtc != null),

            "Revoked" => items.Where(i =>
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc != null),

            "Expired" => items.Where(i =>
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc == null &&
                i.ExpiresAtUtc <= now),

            "Pending" => items.Where(i =>
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc == null &&
                i.ExpiresAtUtc > now),

            _ => items
        };

        var total = await items.CountAsync();

        var rows = await items
            .OrderByDescending(i => i.CreatedAtUtc)
            .ThenBy(i => i.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new InvitationListResponse
        {
            Items = rows.Select(i => Map(i, now))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    // API-015
    public async Task<InvitationOperationResponse>
        IssueAsync(
            Guid actorId,
            IssueInvitationRequest request)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var actor = await GetActorAsync(actorId);

        if (actor is null ||
            !CanManageRole(actor, request.Role))
        {
            return Fail("forbidden",
                "Invitation role is not permitted.");
        }

        var email = request.Email.Trim();

        var normalizedEmail =
            _users.NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return Fail("invalid_email", "Invalid email.");

        var pending = await _db.AccountInvitations
            .AnyAsync(i =>
                i.NormalizedEmail == normalizedEmail &&
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc == null);

        if (pending)
        {
            return Fail("existing_invitation",
                "An unused invitation exists. Reissue it instead.");
        }

        var existing = await _users.FindByEmailAsync(email);

        if (existing is not null)
        {
            var roles = await _users.GetRolesAsync(existing);

            if (roles.Count != 1 ||
                roles[0] != request.Role ||
                !existing.IsActive)
            {
                return Fail("account_conflict",
                    "Invitation cannot be issued for this account.");
            }
        }

        var now = DateTime.UtcNow;
        var token = InvitationTokenService.GenerateToken();

        var invitation = new AccountInvitation
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = request.Role,
            TokenHash =
                InvitationTokenService.HashToken(token),
            IssuedByUserId = actorId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7)
        };

        _db.AccountInvitations.Add(invitation);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Invitations",
            Action = "InvitationIssued",
            EntityType = "AccountInvitation",
            EntityId = invitation.Id,
            Outcome = "success"
        });

        await _email.QueueInvitationEmailAsync(
            invitation.Email,
            token,
            invitation.ExpiresAtUtc);

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return new InvitationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Invitation created. Email queued.",
            EmailQueued = true,
            Invitation = Map(invitation, now)
        };
    }

    // API-016
    public async Task<InvitationOperationResponse>
        ReissueAsync(
            Guid actorId,
            Guid invitationId,
            ReissueInvitationRequest request)
    {
        if (!ValidReason(request.Reason))
            return Fail("invalid_reason",
                "A valid reason is required.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return Fail("forbidden", "Access denied.");

        var previous = await ManagedInvitations(actor)
            .SingleOrDefaultAsync(i =>
                i.Id == invitationId);

        if (previous is null)
            return Fail("not_found", "Invitation unavailable.");

        if (previous.AcceptedAtUtc.HasValue)
            return Fail("already_accepted",
                "Accepted invitations cannot be reissued.");

        var existing = await _users.FindByEmailAsync(
            previous.Email);

        if (existing is not null)
        {
            var roles = await _users.GetRolesAsync(existing);

            if (!existing.IsActive ||
                roles.Count != 1 ||
                roles[0] != previous.Role)
            {
                return Fail("account_conflict",
                    "Invitation cannot be reissued.");
            }
        }

        var now = DateTime.UtcNow;

        var unused = await _db.AccountInvitations
            .Where(i =>
                i.NormalizedEmail ==
                    previous.NormalizedEmail &&
                i.AcceptedAtUtc == null &&
                i.RevokedAtUtc == null)
            .ToListAsync();

        if (unused.Any(i =>
            !CanManageRole(actor, i.Role)))
        {
            return Fail("forbidden",
                "Invitation reissue is not permitted.");
        }

        foreach (var old in unused)
        {
            old.RevokedAtUtc = now;
            old.RevokedByUserId = actorId;
            old.RevocationReason =
                request.Reason.Trim();
        }

        var token = InvitationTokenService.GenerateToken();

        var invitation = new AccountInvitation
        {
            Email = previous.Email,
            NormalizedEmail = previous.NormalizedEmail,
            Role = previous.Role,
            TokenHash =
                InvitationTokenService.HashToken(token),
            IssuedByUserId = actorId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
            ReissuedFromInvitationId = previous.Id
        };

        _db.AccountInvitations.Add(invitation);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Invitations",
            Action = "InvitationReissued",
            EntityType = "AccountInvitation",
            EntityId = invitation.Id,
            Outcome = "success"
        });

        await _email.QueueInvitationEmailAsync(
            invitation.Email,
            token,
            invitation.ExpiresAtUtc);

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return new InvitationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Invitation reissued. Email queued.",
            EmailQueued = true,
            Invitation = Map(invitation, now)
        };
    }

    // API-017
    public async Task<InvitationOperationResponse>
        RevokeAsync(
            Guid actorId,
            Guid invitationId,
            RevokeInvitationRequest request)
    {
        if (!ValidReason(request.Reason))
            return Fail("invalid_reason",
                "A valid reason is required.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return Fail("forbidden", "Access denied.");

        var invitation = await ManagedInvitations(actor)
            .SingleOrDefaultAsync(i =>
                i.Id == invitationId);

        if (invitation is null)
            return Fail("not_found", "Invitation unavailable.");

        if (invitation.AcceptedAtUtc.HasValue)
            return Fail("already_accepted",
                "Accepted invitations cannot be revoked.");

        var now = DateTime.UtcNow;

        if (!invitation.RevokedAtUtc.HasValue)
        {
            invitation.RevokedAtUtc = now;
            invitation.RevokedByUserId = actorId;
            invitation.RevocationReason =
                request.Reason.Trim();

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorId,
                ActorRole = AppRoles.HRManager,
                Module = "Invitations",
                Action = "InvitationRevoked",
                EntityType = "AccountInvitation",
                EntityId = invitation.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        return new InvitationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Invitation revoked.",
            Invitation = Map(invitation, now)
        };
    }
}
