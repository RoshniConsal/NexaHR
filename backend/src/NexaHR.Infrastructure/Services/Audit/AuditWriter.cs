using System.Text.RegularExpressions;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Audit;

public sealed class AuditWriter : IAuditWriter
{
    private readonly NexaHRDbContext _db;

    public AuditWriter(NexaHRDbContext db)
    {
        _db = db;
    }

    private static string ValidateCode(
        string value,
        int maxLength,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                $"{field} is required.");

        value = value.Trim();

        if (value.Length > maxLength ||
            !Regex.IsMatch(
                value,
                @"\A[A-Za-z][A-Za-z0-9_.-]*\z"))
        {
            throw new ArgumentException(
                $"Invalid audit code: {field}.");
        }

        return value;
    }

    public Guid StageEvent(AuditWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = ValidateCode(
            request.ActorRole, 30, "ActorRole");

        if (role != AppRoles.CEO &&
            role != AppRoles.HRManager &&
            role != AppRoles.Candidate &&
            role != AppRoles.Employee &&
            role != "System")
        {
            throw new ArgumentException(
                "Invalid audit actor role.");
        }

        if (role != "System" &&
            (!request.ActorUserId.HasValue ||
             request.ActorUserId == Guid.Empty))
        {
            throw new ArgumentException(
                "Audit actor identity is required.");
        }

        if (role == "System" &&
            request.ActorUserId.HasValue)
        {
            throw new ArgumentException(
                "Invalid system actor identity.");
        }

        var module = ValidateCode(
            request.Module, 50, "Module");

        var action = ValidateCode(
            request.Action, 100, "Action");

        var entityType = ValidateCode(
            request.EntityType, 50, "EntityType");

        var outcome = ValidateCode(
            request.Outcome, 30, "Outcome");

        if (outcome is not
            ("success" or "failure" or
             "denied" or "pending"))
        {
            throw new ArgumentException(
                "Invalid audit outcome.");
        }

        var reasonCode =
            string.IsNullOrWhiteSpace(request.ReasonCode)
                ? null
                : ValidateCode(
                    request.ReasonCode,
                    100,
                    "ReasonCode");

        var hasEntityId =
            request.EntityId.HasValue &&
            request.EntityId != Guid.Empty;

        var isAllowedEvent =
            outcome == "success" &&
            (
                (module == "Privacy" &&
                 entityType == "ProcessingPolicy" &&
                 action == "ProcessingPolicyRecorded" &&
                 role == AppRoles.HRManager &&
                 hasEntityId &&
                 reasonCode is null)

                ||

                (module == "Privacy" &&
                 entityType == "LegalHold" &&
                 role == AppRoles.HRManager &&
                 hasEntityId &&
                 reasonCode is null &&
                 (
                     action == "LegalHoldPlaced" ||
                     action == "LegalHoldUpdated" ||
                     action == "LegalHoldReleased"
                 ))

                ||

                (module == "Company" &&
                 entityType == "CompanySettings" &&
                 action == "CompanySettingsUpdated" &&
                 role == AppRoles.HRManager &&
                 request.EntityId is null &&
                 reasonCode is null)

                ||

                (module == "Departments" &&
                 entityType == "Department" &&
                 role == AppRoles.HRManager &&
                 hasEntityId &&
                 reasonCode is null &&
                 action is
                    ("DepartmentCreated" or
                     "DepartmentUpdated" or
                     "DepartmentArchived"))

                ||

                (module == "Accounts" &&
                 entityType == "Account" &&
                 action == "AccountStatusChanged" &&
                 role == AppRoles.HRManager &&
                 hasEntityId &&
                 reasonCode is
                    ("AccountActivated" or
                     "AccountDeactivated"))

                ||

                (module == "Invitations" &&
                 entityType == "AccountInvitation" &&
                 hasEntityId &&
                 reasonCode is null &&
                 (
                     (role == AppRoles.HRManager &&
                      action is
                        ("InvitationIssued" or
                         "InvitationReissued" or
                         "InvitationRevoked"))

                     ||

                     (action == "InvitationAccepted" &&
                      AppRoles.All.Contains(role))
                 ))
            );

        if (!isAllowedEvent)
        {
            throw new ArgumentException(
                "Audit event is not approved.");
        }

        // Conservative visibility policy.
        // Only explicitly approved non-personal
        // company and department events are visible.

        var isDepartmentEvent =
            module == "Departments" &&
            entityType == "Department" &&
            role == AppRoles.HRManager &&
            outcome == "success" &&
            request.EntityId.HasValue &&
            request.EntityId != Guid.Empty &&
            action is
                ("DepartmentCreated" or
                 "DepartmentUpdated" or
                 "DepartmentArchived");

        var isCompanyEvent =
            module == "Company" &&
            entityType == "CompanySettings" &&
            role == AppRoles.HRManager &&
            outcome == "success" &&
            action == "CompanySettingsUpdated";

        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),

            ActorUserId = request.ActorUserId,

            ActorRole = role,

            Module = module,

            Action = action,

            EntityType = entityType,

            EntityId = request.EntityId,

            Outcome = outcome,

            ReasonCode = reasonCode,

            IsHrVisible =
                isDepartmentEvent || isCompanyEvent,

            IsCeoVisible =
                isDepartmentEvent || isCompanyEvent,

            CorrelationId =
                request.CorrelationId is Guid correlationId &&
                correlationId != Guid.Empty
                    ? correlationId
                    : Guid.NewGuid(),

            CreatedAtUtc = DateTime.UtcNow
        };

        // Stage only. No independent SaveChanges call.
        // The business operation owns the transaction.
        _db.AuditEvents.Add(auditEvent);

        return auditEvent.Id;
    }
}
