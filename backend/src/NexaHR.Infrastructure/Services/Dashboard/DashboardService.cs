using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Dashboard;
using NexaHR.Application.Interfaces.Dashboard;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public DashboardService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<string?> GetAuthorizedRoleAsync(
        Guid actorId)
    {
        var user = await _users.FindByIdAsync(actorId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return null;

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1)
            return null;

        var role = roles[0];

        return role is AppRoles.CEO
            or AppRoles.HRManager
            or AppRoles.Candidate
            or AppRoles.Employee
            ? role
            : null;
    }

    private static DashboardMetric AvailableMetric(
        string key,
        string label,
        int value)
    {
        return new DashboardMetric
        {
            Key = key,
            Label = label,
            Value = value,
            Status = "available"
        };
    }

    private static DashboardMetric PendingMetric(
        string key,
        string label)
    {
        return new DashboardMetric
        {
            Key = key,
            Label = label,
            Value = null,
            Status = "integration_pending"
        };
    }

    private static PendingActionItem PendingAction(
        string key,
        string label)
    {
        return new PendingActionItem
        {
            Key = key,
            Label = label,
            Count = null,
            DeepLink = null,
            Status = "integration_pending"
        };
    }

    // API-148: Role-specific dashboard
    public async Task<DashboardResponse?> GetDashboardAsync(
        Guid actorId)
    {
        var role = await GetAuthorizedRoleAsync(actorId);

        if (role is null)
            return null;

        var metrics = new List<DashboardMetric>();
        var integrations = new List<string>();

        switch (role)
        {
            case AppRoles.CEO:
            {
                var departmentCount = await _db.Departments
                    .AsNoTracking()
                    .CountAsync(d => !d.IsArchived);

                metrics.Add(AvailableMetric(
                    "active_departments",
                    "Active Departments",
                    departmentCount));

                metrics.Add(PendingMetric(
                    "active_employees",
                    "Active Employees"));

                metrics.Add(PendingMetric(
                    "open_vacancies",
                    "Open Vacancies"));

                metrics.Add(PendingMetric(
                    "pending_vacancy_approvals",
                    "Pending Vacancy Approvals"));

                integrations.AddRange(
                    ["EmployeeManagement", "Recruitment"]);

                break;
            }

            case AppRoles.HRManager:
            {
                var departmentCount = await _db.Departments
                    .AsNoTracking()
                    .CountAsync(d => !d.IsArchived);

                metrics.Add(AvailableMetric(
                    "active_departments",
                    "Active Departments",
                    departmentCount));

                metrics.Add(PendingMetric(
                    "active_employees",
                    "Active Employees"));

                metrics.Add(PendingMetric(
                    "open_vacancies",
                    "Open Vacancies"));

                metrics.Add(PendingMetric(
                    "pending_application_reviews",
                    "Pending Application Reviews"));

                metrics.Add(PendingMetric(
                    "pending_leave_requests",
                    "Pending Leave Requests"));

                metrics.Add(PendingMetric(
                    "pending_attendance_corrections",
                    "Pending Attendance Corrections"));

                integrations.AddRange(
                    ["EmployeeManagement", "Recruitment",
                     "Leave", "Attendance"]);

                break;
            }

            case AppRoles.Candidate:
            {
                metrics.Add(PendingMetric(
                    "my_applications",
                    "My Applications"));

                metrics.Add(PendingMetric(
                    "upcoming_interviews",
                    "Upcoming Interviews"));

                metrics.Add(PendingMetric(
                    "pending_offers",
                    "Pending Offers"));

                integrations.Add("Recruitment");

                break;
            }

            case AppRoles.Employee:
            {
                metrics.Add(PendingMetric(
                    "my_attendance",
                    "My Attendance"));

                metrics.Add(PendingMetric(
                    "my_pending_leave_requests",
                    "My Pending Leave Requests"));

                metrics.Add(PendingMetric(
                    "my_onboarding_tasks",
                    "My Onboarding Tasks"));

                integrations.AddRange(
                    ["Attendance", "Leave", "Onboarding"]);

                break;
            }
        }

        return new DashboardResponse
        {
            Role = role,
            GeneratedAtUtc = DateTime.UtcNow,
            IsPartial = integrations.Count > 0,
            Metrics = metrics,
            PendingIntegrations = integrations
        };
    }

    // API-149: Authorized pending actions
    public async Task<PendingActionsResponse?> GetPendingActionsAsync(
        Guid actorId)
    {
        var role = await GetAuthorizedRoleAsync(actorId);

        if (role is null)
            return null;

        var actions = new List<PendingActionItem>();
        var integrations = new List<string>();

        switch (role)
        {
            case AppRoles.CEO:

                actions.Add(PendingAction(
                    "vacancy_approvals",
                    "Vacancies Awaiting Approval"));

                integrations.Add("Recruitment");

                break;

            case AppRoles.HRManager:

                actions.Add(PendingAction(
                    "application_reviews",
                    "Applications Awaiting Review"));

                actions.Add(PendingAction(
                    "interview_actions",
                    "Pending Interview Actions"));

                actions.Add(PendingAction(
                    "leave_approvals",
                    "Pending Leave Approvals"));

                actions.Add(PendingAction(
                    "attendance_corrections",
                    "Pending Attendance Corrections"));

                integrations.AddRange(
                    ["Recruitment", "Leave", "Attendance"]);

                break;

            case AppRoles.Candidate:

                actions.Add(PendingAction(
                    "offer_responses",
                    "Offers Awaiting My Response"));

                integrations.Add("Recruitment");

                break;

            case AppRoles.Employee:

                actions.Add(PendingAction(
                    "onboarding_tasks",
                    "My Pending Onboarding Tasks"));

                integrations.Add("Onboarding");

                break;
        }

        return new PendingActionsResponse
        {
            Role = role,
            GeneratedAtUtc = DateTime.UtcNow,
            IsPartial = integrations.Count > 0,
            TotalPendingActions = null,
            Actions = actions,
            PendingIntegrations = integrations
        };
    }
}
