using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.Vacancies;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.Vacancies;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Vacancies;

public sealed class VacancyService : IVacancyService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public VacancyService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private async Task<bool> IsHrAsync(Guid actorUserId)
    {
        var user = await _users.FindByIdAsync(
            actorUserId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return false;
        }

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
               roles.Contains(AppRoles.HRManager);
    }

    private static VacancyOperationResponse Fail(
        string code,
        string message)
    {
        return new VacancyOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static bool IsValidText(
        string? value,
        int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Length <= maxLength;
    }

    private static bool IsValidSkills(
        List<string>? skills)
    {
        return skills is not null &&
               skills.Count <= 100 &&
               skills.All(s => IsValidText(s, 200));
    }

    public async Task<VacancyOperationResponse> CreateAsync(
        Guid actorUserId,
        CreateVacancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsHrAsync(actorUserId))
            return Fail("forbidden", "Access denied.");

        if (request is null)
            return Fail("validation_error",
                "Request is required.");

        if (request.DepartmentId == Guid.Empty)
            return Fail("validation_error",
                "Department is required.");

        if (!IsValidText(request.Title, 200))
            return Fail("validation_error",
                "A valid vacancy title is required.");

        var employmentType = request.EmploymentType?.Trim() ?? "";

        if (!VacancyEmploymentTypes.IsValid(employmentType))
            return Fail("validation_error",
                "Invalid employment type.");

        var workMode = request.WorkMode?.Trim() ?? "";

        if (!VacancyWorkModes.IsValid(workMode))
            return Fail("validation_error",
                "Invalid work mode.");

        if (request.Openings <= 0)
            return Fail("validation_error",
                "Openings must be greater than zero.");

        if (!IsValidText(request.Location, 200))
            return Fail("validation_error",
                "A valid location is required.");

        if (!IsValidText(request.Description, 10000))
            return Fail("validation_error",
                "A valid job description is required.");

        if (!IsValidText(request.HiringReason, 2000))
            return Fail("validation_error",
                "A valid hiring reason is required.");

        if (request.MinimumExperienceYears is decimal experience &&
            (experience < 0 ||
             experience > 999.99m ||
             decimal.Round(experience, 2) != experience))
        {
            return Fail("validation_error",
                "Invalid minimum experience.");
        }

        var sriLankaTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(
                "Asia/Colombo"));

        var today = DateOnly.FromDateTime(sriLankaTime);

        if (request.ClosingDate < today)
            return Fail("validation_error",
                "Closing date cannot be in the past.");

        if (!IsValidSkills(request.RequiredSkills) ||
            !IsValidSkills(request.PreferredSkills))
        {
            return Fail("validation_error",
                "Invalid vacancy skills.");
        }

        var requiredSkills = request.RequiredSkills
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferredSkills = request.PreferredSkills
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredSkills.Intersect(
                preferredSkills,
                StringComparer.OrdinalIgnoreCase).Any())
        {
            return Fail("validation_error",
                "A skill cannot be both required and preferred.");
        }

        var department = await _db.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == request.DepartmentId,
                cancellationToken);

        if (department is null)
            return Fail("not_found",
                "Department not found.");

        if (department.IsArchived)
            return Fail("archived",
                "Archived departments cannot be used.");

        var now = DateTime.UtcNow;

        var vacancy = new Vacancy
        {
            DepartmentId = request.DepartmentId,
            CreatedByUserId = actorUserId,
            Status = VacancyStatuses.Draft,
            CreatedAtUtc = now
        };

        var revision = new VacancyRevision
        {
            VacancyId = vacancy.Id,
            Revision = 1,
            DepartmentId = request.DepartmentId,
            Title = request.Title.Trim(),
            EmploymentType = employmentType,
            Openings = request.Openings,
            Location = request.Location.Trim(),
            WorkMode = workMode,
            Description = request.Description.Trim(),
            MinimumExperienceYears =
                request.MinimumExperienceYears,
            ClosingDate = request.ClosingDate,
            HiringReason = request.HiringReason.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now
        };

        var criteria = new List<VacancyCriterion>();

        foreach (var skill in requiredSkills)
        {
            criteria.Add(new VacancyCriterion
            {
                VacancyRevisionId = revision.Id,
                Kind = VacancyCriterionKinds.RequiredSkill,
                Label = skill,
                Weight = 2
            });
        }

        foreach (var skill in preferredSkills)
        {
            criteria.Add(new VacancyCriterion
            {
                VacancyRevisionId = revision.Id,
                Kind = VacancyCriterionKinds.PreferredSkill,
                Label = skill,
                Weight = 1
            });
        }

        _db.Vacancies.Add(vacancy);
        _db.VacancyRevisions.Add(revision);
        _db.VacancyCriteria.AddRange(criteria);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = AppRoles.HRManager,
            Module = "Recruitment",
            Action = "VacancyCreated",
            EntityType = "Vacancy",
            EntityId = vacancy.Id,
            Outcome = "success"
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("concurrency_conflict",
                "Vacancy data changed. Reload and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            return Fail("duplicate_record",
                "A conflicting vacancy record already exists.");
        }

        return new VacancyOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Vacancy draft created successfully.",
            Vacancy = new CreateVacancyResponse
            {
                VacancyId = vacancy.Id,
                VacancyRevisionId = revision.Id,
                Revision = revision.Revision,
                Title = revision.Title,
                Status = vacancy.Status,
                CreatedAtUtc = now
            }
        };
    }
}
