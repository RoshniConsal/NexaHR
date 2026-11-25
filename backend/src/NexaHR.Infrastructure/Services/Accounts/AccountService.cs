using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Accounts;
using NexaHR.Application.Interfaces.Accounts;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;
using System.Data;

namespace NexaHR.Infrastructure.Services.Accounts;

public sealed class AccountService : IAccountService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditWriter _audit;

    public AccountService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditWriter audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    private async Task<ApplicationUser?> GetActorAsync(
        Guid actorId)
    {
        var actor = await _userManager.FindByIdAsync(
            actorId.ToString());

        if (actor is null || !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _userManager.IsLockedOutAsync(actor))
            return null;

        var roles = await _userManager.GetRolesAsync(actor);

        return roles.Count == 1 &&
            roles[0] == AppRoles.HRManager
            ? actor
            : null;
    }

    private IQueryable<AccountResponse> ManagedAccounts(
        ApplicationUser actor)
    {
        return
            from user in _db.Users.AsNoTracking()
            join membership in _db.UserRoles
                on user.Id equals membership.UserId
            join role in _db.Roles
                on membership.RoleId equals role.Id
            where _db.UserRoles.Count(
                m => m.UserId == user.Id) == 1
            where role.Name == AppRoles.Candidate ||
                  role.Name == AppRoles.Employee ||
                  (actor.HasPrivilegedAccess &&
                   (role.Name == AppRoles.CEO ||
                    role.Name == AppRoles.HRManager))
            select new AccountResponse
            {
                AccountId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = role.Name ?? string.Empty,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                HasPrivilegedAccess =
                    user.HasPrivilegedAccess,
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc,
                DeactivatedAtUtc = user.DeactivatedAtUtc
            };
    }

    public async Task<AccountListResponse?> GetAccountsAsync(
        Guid actorId,
        AccountQuery query)
    {
        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return null;

        var accounts = ManagedAccounts(actor);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            accounts = accounts.Where(a =>
                a.FullName.Contains(search) ||
                a.Email.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            accounts = accounts.Where(
                a => a.Role == query.Role);
        }

        if (query.IsActive.HasValue)
        {
            accounts = accounts.Where(
                a => a.IsActive == query.IsActive.Value);
        }

        var total = await accounts.CountAsync();

        var items = await accounts
            .OrderBy(a => a.FullName)
            .ThenBy(a => a.AccountId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new AccountListResponse
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<AccountResponse?> GetAccountAsync(
        Guid actorId,
        Guid accountId)
    {
        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return null;

        return await ManagedAccounts(actor)
            .SingleOrDefaultAsync(
                a => a.AccountId == accountId);
    }

    public async Task<AccountOperationResponse>
        ChangeStatusAsync(
            Guid actorId,
            Guid accountId,
            ChangeAccountStatusRequest request)
    {
        if (!request.IsActive.HasValue ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < 3 ||
            request.Reason.Length > 500)
        {
            return Fail("invalid_request",
                "A valid status and reason are required.");
        }

        var activate = request.IsActive.Value;

        if (actorId == accountId && !activate)
        {
            return Fail("self_deactivation",
                "You cannot deactivate your own account.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var actor = await GetActorAsync(actorId);

        if (actor is null)
            return Fail("forbidden", "Access denied.");

        var permitted = await ManagedAccounts(actor)
            .SingleOrDefaultAsync(
                a => a.AccountId == accountId);

        if (permitted is null)
        {
            return Fail("not_found",
                "Account unavailable.");
        }

        var target = await _userManager.FindByIdAsync(
            accountId.ToString());

        if (target is null)
            return Fail("not_found", "Account unavailable.");

        if (target.IsActive == activate)
        {
            return Fail("no_change",
                "Account already has the requested status.");
        }

        if (!activate && target.HasPrivilegedAccess)
        {
            var remaining = await (
                from user in _db.Users
                join membership in _db.UserRoles
                    on user.Id equals membership.UserId
                join role in _db.Roles
                    on membership.RoleId equals role.Id
                where user.Id != accountId &&
                    user.IsActive &&
                    user.EmailConfirmed &&
                    user.HasPrivilegedAccess &&
                    (user.LockoutEnd == null ||
                     user.LockoutEnd <= DateTimeOffset.UtcNow) &&
                    role.Name == AppRoles.HRManager
                where _db.UserRoles.Count(
                    m => m.UserId == user.Id) == 1
                select user.Id
            ).AnyAsync();

            if (!remaining)
            {
                return Fail("last_administrator",
                    "The last privileged administrator cannot be deactivated.");
            }
        }

        var now = DateTime.UtcNow;

        target.IsActive = activate;
        target.UpdatedAtUtc = now;
        target.DeactivatedAtUtc = activate ? null : now;

        var result =
            await _userManager.UpdateSecurityStampAsync(target);

        if (!result.Succeeded)
        {
            return Fail("update_failed",
                "Account status update failed.");
        }

        await _db.AuthSessions
            .Where(s =>
                s.UserId == accountId &&
                s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(
                    s => s.RevokedAtUtc,
                    (DateTime?)now));

        var account = await ManagedAccounts(actor)
            .SingleAsync(a => a.AccountId == accountId);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Accounts",
            Action = "AccountStatusChanged",
            EntityType = "Account",
            EntityId = accountId,
            Outcome = "success",
            ReasonCode = activate
                ? "AccountActivated"
                : "AccountDeactivated"
        });

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return new AccountOperationResponse
        {
            Success = true,
            Code = "success",
            Message = activate
                ? "Account activated successfully."
                : "Account deactivated successfully.",
            Account = account
        };
    }

    private static AccountOperationResponse Fail(
        string code,
        string message)
    {
        return new AccountOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }
}
