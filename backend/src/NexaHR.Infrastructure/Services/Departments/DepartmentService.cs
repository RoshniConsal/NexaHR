using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NexaHR.Application.DTOs.Departments;
using NexaHR.Application.Interfaces.Departments;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Departments;

public sealed class DepartmentService : IDepartmentService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public DepartmentService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private async Task<bool> IsHrAsync(Guid actorId)
    {
        var user = await _users.FindByIdAsync(actorId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return false;

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
               roles.Contains(AppRoles.HRManager);
    }

    private static DepartmentResponse Map(Department d)
    {
        return new DepartmentResponse
        {
            DepartmentId = d.Id,
            Name = d.Name,
            Description = d.Description,
            IsArchived = d.IsArchived,
            CreatedAtUtc = d.CreatedAtUtc,
            UpdatedAtUtc = d.UpdatedAtUtc,
            ArchivedAtUtc = d.ArchivedAtUtc,
            RowVersion = Convert.ToBase64String(d.RowVersion)
        };
    }

    private static DepartmentOperationResponse Fail(
        string code, string message)
    {
        return new DepartmentOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static DepartmentOperationResponse Ok(
        Department department, string message)
    {
        return new DepartmentOperationResponse
        {
            Success = true,
            Code = "success",
            Message = message,
            Department = Map(department)
        };
    }

    private static byte[]? ParseVersion(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 8 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task<DepartmentListResponse?> GetDepartmentsAsync(
        Guid actorId,
        DepartmentQuery request)
    {
        if (!await IsHrAsync(actorId))
            return null;

        var query = _db.Departments.AsNoTracking();

        if (!request.IncludeArchived)
            query = query.Where(d => !d.IsArchived);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToUpperInvariant();

            query = query.Where(
                d => d.NormalizedName.Contains(search));
        }

        var page = Math.Clamp(request.Page, 1, 100000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var count = await query.CountAsync();

        var records = await query
            .OrderBy(d => d.NormalizedName)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new DepartmentListResponse
        {
            Items = records.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = count
        };
    }

    public async Task<DepartmentOperationResponse> CreateAsync(
        Guid actorId,
        CreateDepartmentRequest request)
    {
        if (!await IsHrAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();

        if (name.Length < 2 ||
            name.Length > 150 ||
            normalizedName.Length > 150)
            return Fail("validation_error",
                "Invalid department name.");

        if (request.Description?.Length > 500)
            return Fail("validation_error",
                "Description is too long.");

        if (await _db.Departments.AnyAsync(
            d => d.NormalizedName == normalizedName))
            return Fail("duplicate_department",
                "Department name already exists.");

        var department = new Department
        {
            Name = name,
            NormalizedName = normalizedName,
            Description = string.IsNullOrWhiteSpace(
                request.Description)
                ? null
                : request.Description.Trim(),
            CreatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Departments.Add(department);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Departments",
            Action = "DepartmentCreated",
            EntityType = "Department",
            EntityId = department.Id,
            Outcome = "success"
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            return Fail("duplicate_department",
                "Department name already exists.");
        }

        return Ok(department,
            "Department created successfully.");
    }

    public async Task<DepartmentOperationResponse> UpdateAsync(
        Guid actorId,
        Guid departmentId,
        UpdateDepartmentRequest request)
    {
        if (!await IsHrAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var department = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department is null)
            return Fail("not_found", "Department not found.");

        if (department.IsArchived)
            return Fail("archived",
                "Archived departments cannot be updated.");

        var version = ParseVersion(request.RowVersion);

        if (version is null)
            return Fail("validation_error", "Invalid RowVersion.");

        if (!department.RowVersion.SequenceEqual(version))
            return Fail("concurrency_conflict",
                "Department changed. Reload and retry.");

        if (request.Name is null &&
            request.Description is null &&
            !request.ClearDescription)
            return Fail("validation_error",
                "No update fields supplied.");

        if (request.ClearDescription &&
            request.Description is not null)
            return Fail("validation_error",
                "Conflicting description instructions.");

        if (request.Description?.Length > 500)
            return Fail("validation_error",
                "Description is too long.");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            var normalizedName = name.ToUpperInvariant();

            if (name.Length < 2 ||
                name.Length > 150 ||
                normalizedName.Length > 150)
                return Fail("validation_error",
                    "Invalid department name.");

            if (await _db.Departments.AnyAsync(d =>
                d.Id != departmentId &&
                d.NormalizedName == normalizedName))
                return Fail("duplicate_department",
                    "Department name already exists.");

            department.Name = name;
            department.NormalizedName = normalizedName;
        }

        if (request.ClearDescription)
            department.Description = null;
        else if (request.Description is not null)
            department.Description =
                string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim();

        department.UpdatedAtUtc = DateTime.UtcNow;
        department.UpdatedByUserId = actorId;

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Departments",
            Action = "DepartmentUpdated",
            EntityType = "Department",
            EntityId = department.Id,
            Outcome = "success"
        });

        _db.Entry(department)
            .Property(d => d.RowVersion)
            .OriginalValue = version;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("concurrency_conflict",
                "Department changed. Reload and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            return Fail("duplicate_department",
                "Department name already exists.");
        }

        return Ok(department,
            "Department updated successfully.");
    }

    public async Task<DepartmentOperationResponse> ArchiveAsync(
        Guid actorId,
        Guid departmentId,
        ArchiveDepartmentRequest request)
    {
        if (!await IsHrAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var department = await _db.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department is null)
            return Fail("not_found", "Department not found.");

        if (department.IsArchived)
            return Fail("already_archived",
                "Department is already archived.");

        var reason = request.Reason.Trim();

        if (reason.Length < 3 || reason.Length > 500)
            return Fail("validation_error",
                "A valid archive reason is required.");

        var version = ParseVersion(request.RowVersion);

        if (version is null)
            return Fail("validation_error", "Invalid RowVersion.");

        if (!department.RowVersion.SequenceEqual(version))
            return Fail("concurrency_conflict",
                "Department changed. Reload and retry.");

        return Fail(
            "reference_validation_pending",
            "Archive is blocked until employee and vacancy references can be verified.");
    }
}
