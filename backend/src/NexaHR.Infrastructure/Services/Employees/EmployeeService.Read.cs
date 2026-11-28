using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using NexaHR.Application.Interfaces.Audit;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Employees;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Employees;

public sealed partial class EmployeeService
{
    private readonly NexaHRDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IDataProtector _emailProtector;
    private readonly UserManager<ApplicationUser> _users;

    public EmployeeService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit,
        IDataProtectionProvider protectionProvider)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _emailProtector = protectionProvider.CreateProtector(
            "NexaHR.AuthEmailOutbox.v1");
    }

    private async Task<string?> GetActorRoleAsync(
        Guid actorId)
    {
        var user = await _users.FindByIdAsync(
            actorId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return null;

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
               AppRoles.All.Contains(roles[0])
            ? roles[0]
            : null;
    }

    private static bool CanRead(string? role)
        => role is AppRoles.HRManager
            or AppRoles.CEO
            or AppRoles.Employee;

    private static EmployeeOperationResult<T> Ok<T>(
        T data)
        => new(true, "Success", "Success.", data);

    private static EmployeeOperationResult<T> Fail<T>(
        string code,
        string message)
        => new(false, code, message);

    private static bool ValidPage(
        int page,
        int pageSize)
        => page >= 1 &&
           pageSize >= 1 &&
           pageSize <= 100 &&
           ((long)page - 1) * pageSize <= int.MaxValue;

    private IQueryable<Employee> VisibleEmployees(
        Guid actorId,
        string role)
    {
        var query = _db.Employees.AsNoTracking();

        return role == AppRoles.Employee
            ? query.Where(e => e.UserId == actorId)
            : query;
    }

    // API-051: Employee directory
    public async Task<
        EmployeeOperationResult<EmployeeDirectoryResponse>>
        GetEmployeesAsync(
            Guid actorId,
            EmployeeDirectoryQuery request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorId);

        if (!CanRead(role))
            return Fail<EmployeeDirectoryResponse>(
                "Forbidden", "Access denied.");

        if (!ValidPage(request.Page, request.PageSize))
            return Fail<EmployeeDirectoryResponse>(
                "ValidationError", "Invalid pagination.");

        var query =
            from employee in VisibleEmployees(actorId, role!)
            join user in _db.Users.AsNoTracking()
                on employee.UserId equals user.Id
            join department in _db.Departments.AsNoTracking()
                on employee.DepartmentId equals department.Id
            select new
            {
                Employee = employee,
                User = user,
                Department = department
            };

        if (request.DepartmentId.HasValue)
            query = query.Where(x =>
                x.Employee.DepartmentId ==
                request.DepartmentId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(x =>
                (x.Employee.IsActive && x.User.IsActive)
                    == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            if (search.Length > 200)
                return Fail<EmployeeDirectoryResponse>(
                    "ValidationError", "Search is too long.");

            query = query.Where(x =>
                x.User.FullName.Contains(search) ||
                x.Employee.Designation.Contains(search) ||
                x.Department.Name.Contains(search));
        }

        var total = await query.CountAsync(
            cancellationToken);

        var pageQuery = query
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Employee.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize);

        IReadOnlyList<object> items;

        if (role == AppRoles.CEO)
        {
            items = await pageQuery
                .Select(x => new EmployeeDirectoryItem(
                    x.Employee.Id,
                    x.User.FullName,
                    AppRoles.Employee,
                    x.Employee.Designation,
                    x.Department.Name,
                    x.Employee.IsActive && x.User.IsActive))
                .ToListAsync(cancellationToken);
        }
        else
        {
            var rows = await pageQuery
                .ToListAsync(cancellationToken);

            items = rows.Select(x =>
                new EmployeeDetailsResponse(
                    x.Employee.Id,
                    x.Employee.UserId,
                    x.Employee.EmployeeNumber,
                    x.User.FullName,
                    x.Employee.DepartmentId,
                    x.Department.Name,
                    x.Employee.Designation,
                    x.Employee.EmploymentType,
                    x.Employee.JoiningDate,
                    x.Employee.WorkEmail,
                    x.Employee.WorkEmailVerifiedAtUtc,
                    x.Employee.IsActive && x.User.IsActive,
                    Convert.ToBase64String(
                        x.Employee.RowVersion)))
                .ToList();
        }

        return Ok(new EmployeeDirectoryResponse(
            items,
            request.Page,
            request.PageSize,
            total));
    }

    // API-052: Employee details
    public async Task<EmployeeOperationResult<object>>
        GetEmployeeAsync(
            Guid actorId,
            Guid employeeId,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorId);

        if (!CanRead(role))
            return Fail<object>(
                "Forbidden", "Access denied.");

        var row = await (
            from employee in VisibleEmployees(actorId, role!)
            join user in _db.Users.AsNoTracking()
                on employee.UserId equals user.Id
            join department in _db.Departments.AsNoTracking()
                on employee.DepartmentId equals department.Id
            where employee.Id == employeeId
            select new
            {
                Employee = employee,
                User = user,
                Department = department
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
            return Fail<object>(
                "NotFound", "Employee not found.");

        var e = row.Employee;

        if (role == AppRoles.CEO)
            return Ok<object>(new EmployeeDirectoryItem(
                e.Id,
                row.User.FullName,
                AppRoles.Employee,
                e.Designation,
                row.Department.Name,
                e.IsActive && row.User.IsActive));

        return Ok<object>(new EmployeeDetailsResponse(
            e.Id,
            e.UserId,
            e.EmployeeNumber,
            row.User.FullName,
            e.DepartmentId,
            row.Department.Name,
            e.Designation,
            e.EmploymentType,
            e.JoiningDate,
            e.WorkEmail,
            e.WorkEmailVerifiedAtUtc,
            e.IsActive && row.User.IsActive,
            Convert.ToBase64String(e.RowVersion)));
    }

    // API-056: Employment history
    public async Task<
        EmployeeOperationResult<EmploymentHistoryResponse>>
        GetEmploymentHistoryAsync(
            Guid actorId,
            Guid employeeId,
            EmploymentHistoryQuery request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorId);

        if (role is not
            (AppRoles.HRManager or AppRoles.Employee))
            return Fail<EmploymentHistoryResponse>(
                "Forbidden", "Access denied.");

        if (!ValidPage(request.Page, request.PageSize))
            return Fail<EmploymentHistoryResponse>(
                "ValidationError", "Invalid pagination.");

        var exists = await VisibleEmployees(actorId, role)
            .AnyAsync(e => e.Id == employeeId,
                cancellationToken);

        if (!exists)
            return Fail<EmploymentHistoryResponse>(
                "NotFound", "Employee not found.");

        var query = _db.EmploymentHistories
            .AsNoTracking()
            .Where(h => h.EmployeeId == employeeId);

        var total = await query.CountAsync(
            cancellationToken);

        var items = await query
            .OrderByDescending(h => h.ChangedAtUtc)
            .ThenByDescending(h => h.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(h => new EmploymentHistoryItem(
                h.Id,
                h.ChangeType,
                h.PreviousValuesJson,
                h.NewValuesJson,
                h.Reason,
                h.ChangedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new EmploymentHistoryResponse(
            items,
            request.Page,
            request.PageSize,
            total));
    }
}
