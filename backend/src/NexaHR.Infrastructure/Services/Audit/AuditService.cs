using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Audit;

public sealed class AuditService : IAuditService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    private const int MaxExportRecords = 10000;

    public AuditService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<string?> GetAuthorizedRoleAsync(
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

        if (roles.Count != 1)
            return null;

        return roles[0] is AppRoles.HRManager or AppRoles.CEO
            ? roles[0]
            : null;
    }

    private IQueryable<AuditEvent> VisibleEvents(
        string role,
        Guid actorId)
    {
        // Additional allowlist: visibility flags alone
        // are not sufficient authorization.

        var query = _db.AuditEvents
            .AsNoTracking()
            .Where(a =>
                a.ActorRole == AppRoles.HRManager &&
                a.Outcome == "success" &&
                (
                    (
                        a.Module == "Departments" &&
                        a.EntityType == "Department" &&
                        a.EntityId != null &&
                        a.EntityId != Guid.Empty &&
                        a.ReasonCode == null &&
                        (
                            a.Action == "DepartmentCreated" ||
                            a.Action == "DepartmentUpdated" ||
                            a.Action == "DepartmentArchived"
                        )
                    )
                    ||
                    (
                        a.Module == "Company" &&
                        a.EntityType == "CompanySettings" &&
                        a.EntityId == null &&
                        a.ReasonCode == null &&
                        a.Action == "CompanySettingsUpdated"
                    )
                    ||
                    (
                        role == AppRoles.HRManager &&
                        a.Module == "Employees" &&
                        a.EntityType == "Employee" &&
                        a.EntityId != null &&
                        a.EntityId != Guid.Empty &&
                        a.ReasonCode == null &&
                        (
                            a.Action == "EmployeeUpdated" ||
                            a.Action == "WorkEmailVerificationRequested"
                        )
                    )
                ));

        return role == AppRoles.CEO
            ? query.Where(a => a.IsCeoVisible)
            : query.Where(a =>
                a.IsHrVisible &&
                a.ActorUserId == actorId);
    }

    private static string? ValidateQuery(
        AuditEventQuery query,
        string role)
    {
        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            return "Invalid date range.";
        }

        if (query.Page < 1 ||
            query.Page > 100000 ||
            query.PageSize < 1 ||
            query.PageSize > 100)
        {
            return "Invalid pagination.";
        }

        if (role == AppRoles.CEO &&
            (query.ActorUserId.HasValue ||
             query.EntityId.HasValue))
        {
            return "Identity-based filters are not permitted.";
        }

        return null;
    }

    private static IQueryable<AuditEvent> ApplyFilters(
        IQueryable<AuditEvent> source,
        AuditEventQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim();
            source = source.Where(a => a.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            source = source.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            var outcome = query.Outcome.Trim();
            source = source.Where(a => a.Outcome == outcome);
        }

        if (query.ActorUserId.HasValue)
        {
            source = source.Where(a =>
                a.ActorUserId == query.ActorUserId.Value);
        }

        if (query.EntityId.HasValue)
        {
            source = source.Where(a =>
                a.EntityId == query.EntityId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value.UtcDateTime;

            source = source.Where(a =>
                a.CreatedAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value.UtcDateTime;

            source = source.Where(a =>
                a.CreatedAtUtc <= to);
        }

        return source;
    }

    private static AuditEventResponse Map(
        AuditEvent item,
        string role)
    {
        var restricted = role == AppRoles.CEO;

        return new AuditEventResponse
        {
            EventId = item.Id,

            ActorUserId = restricted
                ? null
                : item.ActorUserId,

            ActorRole = item.ActorRole,

            Module = item.Module,

            Action = item.Action,

            EntityType = item.EntityType,

            EntityId = restricted
                ? null
                : item.EntityId,

            Outcome = item.Outcome,

            // Do not expose unapproved reason codes.
            ReasonCode = null,

            CorrelationId = restricted
                ? null
                : item.CorrelationId,

            CreatedAtUtc = DateTime.SpecifyKind(
                item.CreatedAtUtc,
                DateTimeKind.Utc),

            IsRedacted = true
        };
    }

    private static AuditOperationResponse<T> Success<T>(
        T data) where T : class
    {
        return new AuditOperationResponse<T>
        {
            Success = true,
            Code = "success",
            Message = "Audit operation completed.",
            Data = data
        };
    }

    private static AuditOperationResponse<T> Failure<T>(
        string code,
        string message) where T : class
    {
        return new AuditOperationResponse<T>
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-152: Filtered audit event list
    public async Task<
        AuditOperationResponse<AuditEventListResponse>>
        GetAuditEventsAsync(
            Guid actorId,
            AuditEventQuery query)
    {
        var role = await GetAuthorizedRoleAsync(actorId);

        if (role is null)
        {
            return Failure<AuditEventListResponse>(
                "forbidden",
                "Audit access is not permitted.");
        }

        var validation = ValidateQuery(query, role);

        if (validation is not null)
        {
            return Failure<AuditEventListResponse>(
                "invalid_query",
                validation);
        }

        var source = ApplyFilters(
            VisibleEvents(role, actorId),
            query);

        var totalCount = await source.CountAsync();

        var items = await source
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return Success(new AuditEventListResponse
        {
            Items = items
                .Select(a => Map(a, role))
                .ToList(),

            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    // API-153: Authorized event detail
    public async Task<
        AuditOperationResponse<AuditEventResponse>>
        GetAuditEventAsync(
            Guid actorId,
            Guid eventId)
    {
        var role = await GetAuthorizedRoleAsync(actorId);

        if (role is null)
        {
            return Failure<AuditEventResponse>(
                "forbidden",
                "Audit access is not permitted.");
        }

        var item = await VisibleEvents(role, actorId)
            .SingleOrDefaultAsync(a => a.Id == eventId);

        if (item is null)
        {
            return Failure<AuditEventResponse>(
                "not_found",
                "Audit event was not found.");
        }

        return Success(Map(item, role));
    }

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;

        var trimmed = value.TrimStart();

        // Prevent spreadsheet formula execution.
        if (
            (trimmed.Length > 0 &&
             "=+-@".Contains(trimmed[0]))
            ||
            (value.Length > 0 &&
             "\t\r\n".Contains(value[0]))
        )
        {
            value = "'" + value;
        }

        return "\"" +
            value.Replace("\"", "\"\"") +
            "\"";
    }

    // API-152: Authorized CSV export
    public async Task<
        AuditOperationResponse<AuditExportResponse>>
        ExportAuditEventsAsync(
            Guid actorId,
            AuditEventQuery query)
    {
        var role = await GetAuthorizedRoleAsync(actorId);

        if (role != AppRoles.HRManager)
        {
            return Failure<AuditExportResponse>(
                "forbidden",
                "Audit export is not permitted.");
        }

        var validation = ValidateQuery(query, role);

        if (validation is not null)
        {
            return Failure<AuditExportResponse>(
                "invalid_query",
                validation);
        }

        var items = await ApplyFilters(
                VisibleEvents(role, actorId),
                query)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(MaxExportRecords + 1)
            .ToListAsync();

        if (items.Count > MaxExportRecords)
        {
            return Failure<AuditExportResponse>(
                "export_limit_exceeded",
                "Narrow the filters before exporting.");
        }

        var csv = new StringBuilder();

        csv.AppendLine(
            "EventId,ActorUserId,ActorRole,Module," +
            "Action,EntityType,EntityId,Outcome," +
            "CreatedAtUtc,CorrelationId");

        foreach (var item in items)
        {
            var row = Map(item, role);

            string?[] values =
            [
                row.EventId.ToString(),
                row.ActorUserId?.ToString(),
                row.ActorRole,
                row.Module,
                row.Action,
                row.EntityType,
                row.EntityId?.ToString(),
                row.Outcome,
                row.CreatedAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                row.CorrelationId?.ToString()
            ];

            csv.AppendLine(
                string.Join(
                    ",",
                    values.Select(CsvEscape)));
        }

        var encoding = new UTF8Encoding(true);

        var content = encoding.GetPreamble()
            .Concat(encoding.GetBytes(csv.ToString()))
            .ToArray();

        return Success(new AuditExportResponse
        {
            Content = content,

            ContentType = "text/csv; charset=utf-8",

            FileName =
                $"audit-events-{DateTime.UtcNow:yyyyMMdd}.csv"
        });
    }
}
