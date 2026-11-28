using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.Employees;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Employees;

public sealed partial class EmployeeService
{
    // API-053: Update employee
    public async Task<
        EmployeeOperationResult<EmployeeDetailsResponse>>
        UpdateEmployeeAsync(
            Guid actorId,
            Guid employeeId,
            UpdateEmployeeRequest request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorId);

        if (role != AppRoles.HRManager)
            return Fail<EmployeeDetailsResponse>(
                "Forbidden", "Access denied.");

        var reason = request.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Length > 1000)
            return Fail<EmployeeDetailsResponse>(
                "ValidationError",
                "A valid update reason is required.");

        byte[] expectedVersion;

        try
        {
            expectedVersion = Convert.FromBase64String(
                request.RowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            return Fail<EmployeeDetailsResponse>(
                "ValidationError", "Invalid row version.");
        }

        if (expectedVersion.Length != 8)
            return Fail<EmployeeDetailsResponse>(
                "ValidationError", "Invalid row version.");

        var designation = request.Designation?.Trim();

        if (designation is not null &&
            (designation.Length == 0 ||
             designation.Length > 200))
            return Fail<EmployeeDetailsResponse>(
                "ValidationError", "Invalid designation.");

        var employmentType =
            request.EmploymentType?.Trim();

        if (employmentType is not null &&
            !VacancyEmploymentTypes.IsValid(employmentType))
            return Fail<EmployeeDetailsResponse>(
                "ValidationError",
                "Invalid employment type.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var employee = await _db.Employees
                .SingleOrDefaultAsync(
                    e => e.Id == employeeId,
                    cancellationToken);

            if (employee is null)
                return Fail<EmployeeDetailsResponse>(
                    "NotFound", "Employee not found.");

            if (!employee.RowVersion
                .SequenceEqual(expectedVersion))
                return Fail<EmployeeDetailsResponse>(
                    "Conflict",
                    "Employee record has changed. Reload it.");

            var user = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    u => u.Id == employee.UserId,
                    cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !employee.IsActive)
                return Fail<EmployeeDetailsResponse>(
                    "Conflict", "Employee is not active.");

            var departmentId =
                request.DepartmentId ??
                employee.DepartmentId;

            var department = await _db.Departments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    d => d.Id == departmentId,
                    cancellationToken);

            if (department is null ||
                department.IsArchived)
                return Fail<EmployeeDetailsResponse>(
                    "ValidationError",
                    "Select an active department.");

            var newDesignation =
                designation ?? employee.Designation;

            var newEmploymentType =
                employmentType ?? employee.EmploymentType;

            if (departmentId == employee.DepartmentId &&
                newDesignation == employee.Designation &&
                newEmploymentType == employee.EmploymentType)
                return Fail<EmployeeDetailsResponse>(
                    "ValidationError",
                    "No employment changes supplied.");

            var previousValues = JsonSerializer.Serialize(
                new
                {
                    employee.DepartmentId,
                    employee.Designation,
                    employee.EmploymentType
                });

            employee.DepartmentId = departmentId;
            employee.Designation = newDesignation;
            employee.EmploymentType = newEmploymentType;
            employee.UpdatedAtUtc = DateTime.UtcNow;
            employee.UpdatedByUserId = actorId;

            _db.Entry(employee)
                .Property(e => e.RowVersion)
                .OriginalValue = expectedVersion;

            var newValues = JsonSerializer.Serialize(
                new
                {
                    employee.DepartmentId,
                    employee.Designation,
                    employee.EmploymentType
                });

            _db.EmploymentHistories.Add(
                new EmploymentHistory
                {
                    EmployeeId = employee.Id,
                    ChangeType = "EmployeeUpdated",
                    PreviousValuesJson = previousValues,
                    NewValuesJson = newValues,
                    Reason = reason,
                    ChangedByUserId = actorId,
                    ChangedAtUtc = DateTime.UtcNow
                });

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorId,
                ActorRole = AppRoles.HRManager,
                Module = "Employees",
                Action = "EmployeeUpdated",
                EntityType = "Employee",
                EntityId = employee.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Ok(new EmployeeDetailsResponse(
                employee.Id,
                employee.UserId,
                employee.EmployeeNumber,
                user.FullName,
                employee.DepartmentId,
                department.Name,
                employee.Designation,
                employee.EmploymentType,
                employee.JoiningDate,
                employee.WorkEmail,
                employee.WorkEmailVerifiedAtUtc,
                employee.IsActive && user.IsActive,
                Convert.ToBase64String(
                    employee.RowVersion)));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _db.ChangeTracker.Clear();

            return Fail<EmployeeDetailsResponse>(
                "Conflict",
                "Employee record has changed. Reload it.");
        }
    }
}
