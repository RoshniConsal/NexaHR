using System.Data;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.Employees;
using NexaHR.Application.Interfaces.Employees;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Employees;

public sealed partial class EmployeeService : IEmployeeService
{
    private static bool TryNormalizeWorkEmail(
        string? value,
        out string email,
        out string normalized)
    {
        email = value?.Trim() ?? string.Empty;
        normalized = string.Empty;

        if (email.Length == 0 || email.Length > 256)
            return false;

        if (!MailAddress.TryCreate(email, out var address) ||
            address.Address != email)
            return false;

        normalized = email.ToUpperInvariant();

        return true;
    }

    // API-054: Request work email verification
    public async Task<
        EmployeeOperationResult<WorkEmailVerificationResponse>>
        RequestWorkEmailVerificationAsync(
            Guid actorId,
            Guid employeeId,
            RequestWorkEmailVerification request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorId);

        if (role != AppRoles.HRManager)
            return Fail<WorkEmailVerificationResponse>(
                "Forbidden", "Access denied.");

        if (!TryNormalizeWorkEmail(
            request.WorkEmail,
            out var email,
            out var normalized))
            return Fail<WorkEmailVerificationResponse>(
                "ValidationError", "Invalid work email.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var employee = await _db.Employees
            .SingleOrDefaultAsync(
                e => e.Id == employeeId,
                cancellationToken);

        if (employee is null)
            return Fail<WorkEmailVerificationResponse>(
                "NotFound", "Employee not found.");

        var activeUser = await _db.Users.AnyAsync(
            u => u.Id == employee.UserId && u.IsActive,
            cancellationToken);

        if (!employee.IsActive || !activeUser)
            return Fail<WorkEmailVerificationResponse>(
                "Conflict", "Employee is not active.");

        if (employee.NormalizedWorkEmail == normalized &&
            employee.WorkEmailVerifiedAtUtc.HasValue)
            return Fail<WorkEmailVerificationResponse>(
                "Conflict", "Work email is already verified.");

        var duplicate = await _db.Employees.AnyAsync(
            e => e.Id != employeeId &&
                 e.NormalizedWorkEmail == normalized,
            cancellationToken);

        var loginEmailExists = await _db.Users.AnyAsync(
            u => u.NormalizedEmail == normalized,
            cancellationToken);

        if (duplicate || loginEmailExists)
            return Fail<WorkEmailVerificationResponse>(
                "Conflict", "Work email is unavailable.");

        var now = DateTime.UtcNow;

        var pending = await _db.WorkEmailVerifications
            .Where(v =>
                v.EmployeeId == employeeId &&
                v.ConfirmedAtUtc == null &&
                v.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var previous in pending)
            previous.RevokedAtUtc = now;

        // Release the filtered unique index before
        // inserting the replacement verification.
        // Both saves remain inside the same transaction.
        if (pending.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        var token = WorkEmailToken.Generate();

        var verification = new WorkEmailVerification
        {
            EmployeeId = employeeId,
            ProposedEmail = email,
            NormalizedProposedEmail = normalized,
            TokenHash = WorkEmailToken.Hash(token),
            RequestedByUserId = actorId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(1)
        };

        _db.WorkEmailVerifications.Add(verification);

        _db.AuthEmailOutbox.Add(new AuthEmailOutbox
        {
            RecipientEmail = email,
            EmailType = "WorkEmailVerification",
            ProtectedToken = _emailProtector.Protect(token),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            ExpiresAtUtc = verification.ExpiresAtUtc,
            AttemptCount = 0
        });

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Employees",
            Action = "WorkEmailVerificationRequested",
            EntityType = "Employee",
            EntityId = employeeId,
            Outcome = "success"
        });

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new WorkEmailVerificationResponse(
            verification.Id,
            verification.ExpiresAtUtc));
    }

    // API-055: Confirm work email verification
    public async Task<EmployeeOperationResult<bool>>
        ConfirmWorkEmailVerificationAsync(
            ConfirmWorkEmailVerification request,
            CancellationToken cancellationToken = default)
    {
        if (!WorkEmailToken.TryHash(
            request.Token, out var hash))
            return Fail<bool>(
                "ValidationError",
                "Invalid or expired verification token.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            var verification =
                await _db.WorkEmailVerifications
                    .SingleOrDefaultAsync(
                        v => v.TokenHash == hash,
                        cancellationToken);

            if (verification is null ||
                verification.ConfirmedAtUtc.HasValue ||
                verification.RevokedAtUtc.HasValue ||
                verification.ExpiresAtUtc <= now)
                return Fail<bool>(
                    "ValidationError",
                    "Invalid or expired verification token.");

            var employee = await _db.Employees
                .SingleOrDefaultAsync(
                    e => e.Id == verification.EmployeeId,
                    cancellationToken);

            if (employee is null || !employee.IsActive)
                return Fail<bool>(
                    "ValidationError",
                    "Verification cannot be completed.");

            var activeUser = await _db.Users.AnyAsync(
                u => u.Id == employee.UserId && u.IsActive,
                cancellationToken);

            if (!activeUser)
                return Fail<bool>(
                    "ValidationError",
                    "Verification cannot be completed.");

            var duplicate = await _db.Employees.AnyAsync(
                e => e.Id != employee.Id &&
                     e.NormalizedWorkEmail ==
                         verification.NormalizedProposedEmail,
                cancellationToken);

            var loginEmailExists = await _db.Users.AnyAsync(
                u => u.NormalizedEmail ==
                    verification.NormalizedProposedEmail,
                cancellationToken);

            if (duplicate || loginEmailExists)
                return Fail<bool>(
                    "Conflict", "Work email is unavailable.");

            var previousValues = JsonSerializer.Serialize(
                new
                {
                    employee.WorkEmail,
                    employee.WorkEmailVerifiedAtUtc
                });

            employee.WorkEmail =
                verification.ProposedEmail;

            employee.NormalizedWorkEmail =
                verification.NormalizedProposedEmail;

            employee.WorkEmailVerifiedAtUtc = now;
            employee.UpdatedAtUtc = now;
            employee.UpdatedByUserId = null;

            verification.ConfirmedAtUtc = now;

            _db.EmploymentHistories.Add(
                new EmploymentHistory
                {
                    EmployeeId = employee.Id,
                    ChangeType = "WorkEmailVerified",
                    PreviousValuesJson = previousValues,
                    NewValuesJson = JsonSerializer.Serialize(
                        new
                        {
                            employee.WorkEmail,
                            employee.WorkEmailVerifiedAtUtc
                        }),
                    Reason =
                        "Work email ownership verified by token holder. " +
                        "ChangedByUserId identifies the requesting HR user.",
                    ChangedByUserId =
                        verification.RequestedByUserId,
                    ChangedAtUtc = now
                });

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorRole = "System",
                Module = "Employees",
                Action = "WorkEmailVerified",
                EntityType = "Employee",
                EntityId = employee.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Ok(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _db.ChangeTracker.Clear();

            return Fail<bool>(
                "Conflict",
                "Verification state has changed.");
        }
    }
}
