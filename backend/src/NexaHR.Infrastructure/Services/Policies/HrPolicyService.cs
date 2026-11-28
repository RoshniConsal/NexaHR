using Microsoft.AspNetCore.Identity;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Application.Interfaces.Policies;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Policies;

public sealed partial class HrPolicyService
    : IHrPolicyService
{
    private readonly NexaHRDbContext _db;

    private readonly UserManager<ApplicationUser> _users;

    private readonly IAuditWriter _audit;

    public HrPolicyService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private async Task<string?> GetActorRoleAsync(
        Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
            return null;

        var user = await _users.FindByIdAsync(
            actorUserId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return null;
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1)
            return null;

        var role = roles[0];

        return AppRoles.All.Contains(role)
            ? role
            : null;
    }

    private static bool CanRead(string? role)
    {
        return role == AppRoles.HRManager ||
               role == AppRoles.CEO ||
               role == AppRoles.Employee;
    }

    private static bool CanManage(string? role)
    {
        return role == AppRoles.HRManager;
    }

    private static bool CanAcknowledge(string? role)
    {
        return role == AppRoles.HRManager ||
               role == AppRoles.Employee;
    }

    private static bool IsVisible(
        HrPolicyVersion version,
        string? role)
    {
        return role switch
        {
            AppRoles.HRManager => true,
            AppRoles.Employee => version.VisibleToEmployees,
            AppRoles.CEO => version.VisibleToCeo,
            _ => false
        };
    }

    private static bool IsAcknowledgementAllowed(
        HrPolicyVersion version,
        string? role)
    {
        if (!version.RequiresAcknowledgement)
            return false;

        return role switch
        {
            AppRoles.HRManager => version.VisibleToHr,
            AppRoles.Employee => version.VisibleToEmployees,
            _ => false
        };
    }

    private static bool ValidPage(
        int page,
        int pageSize)
    {
        return page >= 1 &&
               pageSize >= 1 &&
               pageSize <= 100 &&
               ((long)page - 1) * pageSize <= int.MaxValue;
    }

    private static bool TryParseRowVersion(
        string? value,
        out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            rowVersion = Convert.FromBase64String(value);

            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? ValidateDraft(
        CreatePolicyRequest request)
    {
        var title = request.Title?.Trim();
        var content = request.Content?.Trim();

        if (string.IsNullOrWhiteSpace(title) ||
            title.Length > 200)
        {
            return "Policy title must contain 1 to 200 characters.";
        }

        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > 50000)
        {
            return "Policy content must contain 1 to 50000 characters.";
        }

        if (!request.VisibleToEmployees &&
            !request.VisibleToHr &&
            !request.VisibleToCeo)
        {
            return "At least one policy audience is required.";
        }

        if (request.RequiresAcknowledgement &&
            !request.VisibleToEmployees &&
            !request.VisibleToHr)
        {
            return "Acknowledgement requires an eligible audience.";
        }

        return null;
    }

    private static void ApplyDraft(
        HrPolicy policy,
        CreatePolicyRequest request)
    {
        policy.DraftTitle = request.Title.Trim();

        policy.DraftContent = request.Content.Trim();

        policy.DraftRequiresAcknowledgement =
            request.RequiresAcknowledgement;

        policy.DraftVisibleToEmployees =
            request.VisibleToEmployees;

        policy.DraftVisibleToHr =
            request.VisibleToHr;

        policy.DraftVisibleToCeo =
            request.VisibleToCeo;

        policy.HasDraft = true;
    }

    private static PolicyOperationResult<T> Ok<T>(
        T data,
        string message = "Success")
    {
        return new PolicyOperationResult<T>(
            true,
            "Success",
            message,
            data);
    }

    private static PolicyOperationResult<T> Fail<T>(
        string code,
        string message)
    {
        return new PolicyOperationResult<T>(
            false,
            code,
            message);
    }
}
