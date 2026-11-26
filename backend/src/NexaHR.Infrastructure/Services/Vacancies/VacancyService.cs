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

    private static bool IsSqlDeadlock(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is SqlException sql &&
                sql.Number == 1205)
            {
                return true;
            }
        }

        return false;
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

    private async Task<bool> IsCeoAsync(Guid actorUserId)
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
               roles.Contains(AppRoles.CEO);
    }

    private async Task<bool> CanViewVacanciesAsync(
        Guid actorUserId)
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
               (roles.Contains(AppRoles.HRManager) ||
                roles.Contains(AppRoles.CEO));
    }

    // API-063: List vacancies
    public async Task<VacancyListResponse?> GetVacanciesAsync(
        Guid actorUserId,
        VacancyQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanViewVacanciesAsync(actorUserId))
            return null;

        var query =
            from vacancy in _db.Vacancies.AsNoTracking()
            join revision in _db.VacancyRevisions.AsNoTracking()
                on vacancy.Id equals revision.VacancyId
            join department in _db.Departments.AsNoTracking()
                on revision.DepartmentId equals department.Id
            where !_db.VacancyRevisions.Any(r =>
                r.VacancyId == vacancy.Id &&
                r.Revision > revision.Revision)
            select new
            {
                Vacancy = vacancy,
                Revision = revision,
                Department = department
            };

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();

            query = query.Where(
                x => x.Vacancy.Status == status);
        }

        if (request.DepartmentId.HasValue)
        {
            var departmentId = request.DepartmentId.Value;

            query = query.Where(
                x => x.Revision.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(
                x => x.Revision.Title.Contains(search));
        }

        var page = Math.Clamp(request.Page, 1, 100000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var totalCount = await query.CountAsync(
            cancellationToken);

        var records = await query
            .OrderByDescending(x => x.Vacancy.CreatedAtUtc)
            .ThenBy(x => x.Vacancy.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new VacancyListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = records.Select(x =>
                new VacancyListItemResponse
                {
                    VacancyId = x.Vacancy.Id,
                    VacancyRevisionId = x.Revision.Id,
                    Revision = x.Revision.Revision,
                    DepartmentId = x.Revision.DepartmentId,
                    DepartmentName = x.Department.Name,
                    Title = x.Revision.Title,
                    Status = x.Vacancy.Status,
                    EmploymentType = x.Revision.EmploymentType,
                    Location = x.Revision.Location,
                    WorkMode = x.Revision.WorkMode,
                    Openings = x.Revision.Openings,
                    ClosingDate = x.Revision.ClosingDate,
                    CreatedAtUtc = x.Vacancy.CreatedAtUtc,
                    UpdatedAtUtc = x.Vacancy.UpdatedAtUtc,
                    SubmittedAtUtc = x.Revision.SubmittedAtUtc,
                    PublishedAtUtc = x.Vacancy.PublishedAtUtc,
                    RowVersion = Convert.ToBase64String(
                        x.Vacancy.RowVersion)
                }).ToList()
        };
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

    private async Task<VacancyOperationResponse?> ValidateVacancyAsync(
        CreateVacancyRequest request,
        CancellationToken cancellationToken)
    {
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

        return null;
    }

    public async Task<VacancyOperationResponse> CreateAsync(
        Guid actorUserId,
        CreateVacancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsHrAsync(actorUserId))
            return Fail("forbidden", "Access denied.");

        var validationError = await ValidateVacancyAsync(
            request,
            cancellationToken);

        if (validationError is not null)
            return validationError;

        var employmentType = request.EmploymentType.Trim();
        var workMode = request.WorkMode.Trim();

        var requiredSkills = request.RequiredSkills
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferredSkills = request.PreferredSkills
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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

    // API-065: Get vacancy details
    public async Task<VacancyDetailsResponse?> GetVacancyDetailsAsync(
        Guid actorUserId,
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanViewVacanciesAsync(actorUserId))
        {
            throw new UnauthorizedAccessException(
                "Vacancy access is not permitted.");
        }

        var record = await (
            from vacancy in _db.Vacancies.AsNoTracking()

            join revision in _db.VacancyRevisions.AsNoTracking()
                on vacancy.Id equals revision.VacancyId

            join department in _db.Departments.AsNoTracking()
                on revision.DepartmentId equals department.Id

            where vacancy.Id == vacancyId

            orderby revision.Revision descending

            select new
            {
                Vacancy = vacancy,
                Revision = revision,
                Department = department
            }
        ).FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return null;
        }

        var criteria = await _db.VacancyCriteria
            .AsNoTracking()
            .Where(c => c.VacancyRevisionId == record.Revision.Id)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Label)
            .Select(c => new VacancyCriterionResponse
            {
                CriterionId = c.Id,
                Kind = c.Kind,
                Label = c.Label,
                Weight = c.Weight
            })
            .ToListAsync(cancellationToken);

        var decision = await _db.VacancyDecisions
            .AsNoTracking()
            .Where(d => d.VacancyRevisionId == record.Revision.Id)
            .Select(d => new VacancyDecisionResponse
            {
                DecisionId = d.Id,
                VacancyRevisionId = d.VacancyRevisionId,
                ApproverUserId = d.ApproverUserId,
                ApproverRole = d.ApproverRole,
                Decision = d.Decision,
                Comment = d.Comment,
                AtUtc = d.AtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new VacancyDetailsResponse
        {
            VacancyId = record.Vacancy.Id,
            VacancyRevisionId = record.Revision.Id,
            Revision = record.Revision.Revision,

            DepartmentId = record.Revision.DepartmentId,
            DepartmentName = record.Department.Name,

            Title = record.Revision.Title,
            Status = record.Vacancy.Status,
            EmploymentType = record.Revision.EmploymentType,
            Openings = record.Revision.Openings,
            Location = record.Revision.Location,
            WorkMode = record.Revision.WorkMode,
            Description = record.Revision.Description,

            MinimumExperienceYears =
                record.Revision.MinimumExperienceYears,

            ClosingDate = record.Revision.ClosingDate,
            HiringReason = record.Revision.HiringReason,

            RequiredSkills = criteria
                .Where(c => c.Kind == VacancyCriterionKinds.RequiredSkill)
                .Select(c => c.Label)
                .ToList(),

            PreferredSkills = criteria
                .Where(c => c.Kind == VacancyCriterionKinds.PreferredSkill)
                .Select(c => c.Label)
                .ToList(),

            Criteria = criteria,

            CreatedByUserId = record.Vacancy.CreatedByUserId,
            CreatedAtUtc = record.Vacancy.CreatedAtUtc,
            UpdatedAtUtc = record.Vacancy.UpdatedAtUtc,

            RevisionCreatedAtUtc = record.Revision.CreatedAtUtc,

            SubmittedByUserId = record.Revision.SubmittedByUserId,
            SubmittedAtUtc = record.Revision.SubmittedAtUtc,

            Decision = decision,

            PublishedRevisionId = record.Vacancy.PublishedRevisionId,
            PublishedByUserId = record.Vacancy.PublishedByUserId,
            PublishedAtUtc = record.Vacancy.PublishedAtUtc,

            ClosedByUserId = record.Vacancy.ClosedByUserId,
            ClosedAtUtc = record.Vacancy.ClosedAtUtc,
            ClosureReason = record.Vacancy.ClosureReason,

            RowVersion = Convert.ToBase64String(
                record.Vacancy.RowVersion),

            RevisionRowVersion = Convert.ToBase64String(
                record.Revision.RowVersion)
        };
    }

    private static UpdateVacancyOperationResponse UpdateFail(
        string code,
        string message)
    {
        return new UpdateVacancyOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static bool TryReadRowVersion(
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

    // API-066: Update vacancy and manage revisions
    public async Task<UpdateVacancyOperationResponse> UpdateAsync(
        Guid actorUserId,
        Guid vacancyId,
        UpdateVacancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsHrAsync(actorUserId))
            return UpdateFail(
                "forbidden",
                "Access denied.");

        if (request is null ||
            vacancyId == Guid.Empty ||
            request.VacancyRevisionId == Guid.Empty)
        {
            return UpdateFail(
                "validation_error",
                "Vacancy and revision IDs are required.");
        }

        if (!TryReadRowVersion(
                request.RowVersion,
                out var vacancyRowVersion) ||
            !TryReadRowVersion(
                request.RevisionRowVersion,
                out var revisionRowVersion))
        {
            return UpdateFail(
                "validation_error",
                "Valid row versions are required.");
        }

        var validationRequest = new CreateVacancyRequest
        {
            DepartmentId = request.DepartmentId,
            Title = request.Title,
            EmploymentType = request.EmploymentType,
            Openings = request.Openings,
            Location = request.Location,
            WorkMode = request.WorkMode,
            Description = request.Description,
            MinimumExperienceYears =
                request.MinimumExperienceYears,
            ClosingDate = request.ClosingDate,
            HiringReason = request.HiringReason,
            RequiredSkills = request.RequiredSkills,
            PreferredSkills = request.PreferredSkills
        };

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            var vacancy = await _db.Vacancies
                .FirstOrDefaultAsync(
                    v => v.Id == vacancyId,
                    cancellationToken);

            if (vacancy is null)
                return UpdateFail(
                    "not_found",
                    "Vacancy not found.");

            var currentRevision = await _db.VacancyRevisions
                .Where(r => r.VacancyId == vacancyId)
                .OrderByDescending(r => r.Revision)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentRevision is null)
                return UpdateFail(
                    "invalid_state",
                    "Vacancy revision is missing.");

            if (currentRevision.Id != request.VacancyRevisionId ||
                !vacancy.RowVersion.SequenceEqual(
                    vacancyRowVersion) ||
                !currentRevision.RowVersion.SequenceEqual(
                    revisionRowVersion))
            {
                return UpdateFail(
                    "concurrency_conflict",
                    "Vacancy data changed. Reload and retry.");
            }

            if (vacancy.Status is
                VacancyStatuses.PendingApproval or
                VacancyStatuses.Closed)
            {
                return UpdateFail(
                    "invalid_state",
                    "Vacancy cannot be edited in its current status.");
            }

            var createNewRevision = vacancy.Status is
                VacancyStatuses.Rejected or
                VacancyStatuses.Approved or
                VacancyStatuses.Published;

            var publicationPaused =
                vacancy.Status == VacancyStatuses.Published;

            if (!createNewRevision &&
                vacancy.Status != VacancyStatuses.Draft &&
                vacancy.Status != VacancyStatuses.Paused)
            {
                return UpdateFail(
                    "invalid_state",
                    "Unsupported vacancy status.");
            }

            if (createNewRevision !=
                currentRevision.SubmittedAtUtc.HasValue)
            {
                return UpdateFail(
                    "invalid_state",
                    "Vacancy revision state is inconsistent.");
            }

            var validationError =
                await ValidateVacancyAsync(
                    validationRequest,
                    cancellationToken);

            if (validationError is not null)
            {
                return UpdateFail(
                    validationError.Code,
                    validationError.Message);
            }

            // Conservative capacity protection until
            // offer and employee activation integration.
            var approvedOpenings = await (
                from revision in _db.VacancyRevisions
                join decision in _db.VacancyDecisions
                    on revision.Id equals decision.VacancyRevisionId
                where revision.VacancyId == vacancyId &&
                      decision.Decision ==
                          VacancyDecisionTypes.Approved
                orderby revision.Revision descending
                select (int?)revision.Openings
            ).FirstOrDefaultAsync(cancellationToken);

            if (approvedOpenings.HasValue &&
                request.Openings < approvedOpenings.Value)
            {
                return UpdateFail(
                    "capacity_validation_pending",
                    "Approved openings cannot be reduced until " +
                    "hiring capacity validation is available.");
            }

            var existingCriteria = await _db.VacancyCriteria
                .Where(c =>
                    c.VacancyRevisionId == currentRevision.Id)
                .ToListAsync(cancellationToken);

            var requiredSkills = request.RequiredSkills
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preferredSkills = request.PreferredSkills
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var now = DateTime.UtcNow;

            var targetRevision = currentRevision;

            if (createNewRevision)
            {
                targetRevision = new VacancyRevision
                {
                    VacancyId = vacancy.Id,
                    Revision = currentRevision.Revision + 1,
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                };

                _db.VacancyRevisions.Add(targetRevision);

                // Preserve non-skill criteria in the new revision.
                foreach (var criterion in existingCriteria.Where(c =>
                    c.Kind != VacancyCriterionKinds.RequiredSkill &&
                    c.Kind != VacancyCriterionKinds.PreferredSkill))
                {
                    _db.VacancyCriteria.Add(
                        new VacancyCriterion
                        {
                            VacancyRevisionId = targetRevision.Id,
                            Kind = criterion.Kind,
                            Label = criterion.Label,
                            Weight = criterion.Weight
                        });
                }
            }

            targetRevision.DepartmentId = request.DepartmentId;
            targetRevision.Title = request.Title.Trim();
            targetRevision.EmploymentType =
                request.EmploymentType.Trim();
            targetRevision.Openings = request.Openings;
            targetRevision.Location = request.Location.Trim();
            targetRevision.WorkMode = request.WorkMode.Trim();
            targetRevision.Description = request.Description.Trim();
            targetRevision.MinimumExperienceYears =
                request.MinimumExperienceYears;
            targetRevision.ClosingDate = request.ClosingDate;
            targetRevision.HiringReason = request.HiringReason.Trim();

            var desiredCriteria =
                new List<VacancyCriterion>();

            foreach (var skill in requiredSkills)
            {
                desiredCriteria.Add(new VacancyCriterion
                {
                    VacancyRevisionId = targetRevision.Id,
                    Kind = VacancyCriterionKinds.RequiredSkill,
                    Label = skill,
                    Weight = 2
                });
            }

            foreach (var skill in preferredSkills)
            {
                desiredCriteria.Add(new VacancyCriterion
                {
                    VacancyRevisionId = targetRevision.Id,
                    Kind = VacancyCriterionKinds.PreferredSkill,
                    Label = skill,
                    Weight = 1
                });
            }

            if (createNewRevision)
            {
                _db.VacancyCriteria.AddRange(desiredCriteria);
            }
            else
            {
                var oldSkills = existingCriteria
                    .Where(c =>
                        c.Kind == VacancyCriterionKinds.RequiredSkill ||
                        c.Kind == VacancyCriterionKinds.PreferredSkill)
                    .ToList();

                foreach (var oldSkill in oldSkills)
                {
                    var matching = desiredCriteria.FirstOrDefault(c =>
                        c.Kind == oldSkill.Kind &&
                        string.Equals(
                            c.Label,
                            oldSkill.Label,
                            StringComparison.OrdinalIgnoreCase));

                    if (matching is null)
                    {
                        _db.VacancyCriteria.Remove(oldSkill);
                    }
                    else
                    {
                        oldSkill.Label = matching.Label;
                        oldSkill.Weight = matching.Weight;

                        desiredCriteria.Remove(matching);
                    }
                }

                _db.VacancyCriteria.AddRange(desiredCriteria);

                // Ensure revision row version changes even
                // when only skill criteria are modified.
                _db.Entry(targetRevision)
                    .Property(r => r.Title)
                    .IsModified = true;
            }

            vacancy.DepartmentId = request.DepartmentId;
            vacancy.UpdatedAtUtc = now;

            if (publicationPaused ||
                vacancy.Status == VacancyStatuses.Paused)
            {
                vacancy.Status = VacancyStatuses.Paused;
            }
            else
            {
                vacancy.Status = VacancyStatuses.Draft;
            }

            // Force aggregate concurrency check.
            _db.Entry(vacancy)
                .Property(v => v.UpdatedAtUtc)
                .IsModified = true;

            var correlationId = Guid.NewGuid();

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = createNewRevision
                    ? "VacancyRevisionCreated"
                    : "VacancyUpdated",
                EntityType = "Vacancy",
                EntityId = vacancy.Id,
                Outcome = "success",
                CorrelationId = correlationId
            });

            if (publicationPaused)
            {
                _audit.StageEvent(new AuditWriteRequest
                {
                    ActorUserId = actorUserId,
                    ActorRole = AppRoles.HRManager,
                    Module = "Recruitment",
                    Action = "VacancyPublicationPaused",
                    EntityType = "Vacancy",
                    EntityId = vacancy.Id,
                    Outcome = "success",
                    CorrelationId = correlationId
                });
            }

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new UpdateVacancyOperationResponse
            {
                Success = true,
                Code = "success",
                Message = createNewRevision
                    ? "New vacancy revision created successfully."
                    : "Vacancy draft updated successfully.",
                Vacancy = new UpdateVacancyResponse
                {
                    VacancyId = vacancy.Id,
                    VacancyRevisionId = targetRevision.Id,
                    Revision = targetRevision.Revision,
                    Title = targetRevision.Title,
                    Status = vacancy.Status,
                    NewRevisionCreated = createNewRevision,
                    PublicationPaused = publicationPaused,
                    UpdatedAtUtc = now,
                    RowVersion = Convert.ToBase64String(
                        vacancy.RowVersion),
                    RevisionRowVersion = Convert.ToBase64String(
                        targetRevision.RowVersion)
                }
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateFail(
                "concurrency_conflict",
                "Vacancy data changed. Reload and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            return UpdateFail(
                "concurrency_conflict",
                "Conflicting vacancy update. Reload and retry.");
        }
        catch (SqlException ex)
            when (ex.Number == 1205)
        {
            return UpdateFail(
                "concurrency_conflict",
                "Concurrent vacancy update. Reload and retry.");
        }
        catch (InvalidOperationException ex)
            when (IsSqlDeadlock(ex))
        {
            return UpdateFail(
                "concurrency_conflict",
                "Concurrent vacancy update. Reload and retry.");
        }
    }

    public async Task<VacancyRevisionHistoryResponse?>
        GetRevisionHistoryAsync(
            Guid actorUserId,
            Guid vacancyId,
            CancellationToken cancellationToken = default)
    {
        if (!await CanViewVacanciesAsync(actorUserId))
        {
            throw new UnauthorizedAccessException();
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

        var vacancy = await _db.Vacancies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                v => v.Id == vacancyId,
                cancellationToken);

        if (vacancy is null)
        {
            return null;
        }

        var revisions = await (
            from revision in _db.VacancyRevisions.AsNoTracking()
            join department in _db.Departments.AsNoTracking()
                on revision.DepartmentId equals department.Id
            where revision.VacancyId == vacancyId
            orderby revision.Revision descending
            select new
            {
                Revision = revision,
                DepartmentName = department.Name
            })
            .ToListAsync(cancellationToken);

        if (revisions.Count == 0)
        {
            throw new InvalidOperationException(
                "Vacancy revision history is missing.");
        }

        var currentRevision = revisions[0].Revision;

        var criteria = await (
            from criterion in _db.VacancyCriteria.AsNoTracking()
            join revision in _db.VacancyRevisions.AsNoTracking()
                on criterion.VacancyRevisionId equals revision.Id
            where revision.VacancyId == vacancyId
            orderby criterion.Kind, criterion.Label
            select criterion)
            .ToListAsync(cancellationToken);

        var decisions = await (
            from decision in _db.VacancyDecisions.AsNoTracking()
            join revision in _db.VacancyRevisions.AsNoTracking()
                on decision.VacancyRevisionId equals revision.Id
            where revision.VacancyId == vacancyId
            select decision)
            .ToListAsync(cancellationToken);

        var criteriaByRevision = criteria.ToLookup(
            c => c.VacancyRevisionId);

        var decisionsByRevision = decisions.ToDictionary(
            d => d.VacancyRevisionId);

        var items =
            new List<VacancyRevisionHistoryItemResponse>();

        foreach (var item in revisions)
        {
            var revision = item.Revision;

            var revisionCriteria = criteriaByRevision[
                revision.Id].ToList();

            decisionsByRevision.TryGetValue(
                revision.Id,
                out var decision);

            items.Add(new VacancyRevisionHistoryItemResponse
            {
                VacancyRevisionId = revision.Id,
                Revision = revision.Revision,

                IsCurrent =
                    revision.Id == currentRevision.Id,

                IsPublishedRevision =
                    vacancy.PublishedRevisionId == revision.Id,

                IsSubmitted =
                    revision.SubmittedAtUtc.HasValue,

                DepartmentId = revision.DepartmentId,
                DepartmentName = item.DepartmentName,
                Title = revision.Title,
                EmploymentType = revision.EmploymentType,
                Openings = revision.Openings,
                Location = revision.Location,
                WorkMode = revision.WorkMode,
                Description = revision.Description,

                MinimumExperienceYears =
                    revision.MinimumExperienceYears,

                ClosingDate = revision.ClosingDate,
                HiringReason = revision.HiringReason,

                RequiredSkills = revisionCriteria
                    .Where(c => c.Kind ==
                        VacancyCriterionKinds.RequiredSkill)
                    .Select(c => c.Label)
                    .ToList(),

                PreferredSkills = revisionCriteria
                    .Where(c => c.Kind ==
                        VacancyCriterionKinds.PreferredSkill)
                    .Select(c => c.Label)
                    .ToList(),

                Criteria = revisionCriteria
                    .Select(c => new VacancyCriterionResponse
                    {
                        CriterionId = c.Id,
                        Kind = c.Kind,
                        Label = c.Label,
                        Weight = c.Weight
                    })
                    .ToList(),

                CreatedByUserId = revision.CreatedByUserId,
                CreatedAtUtc = revision.CreatedAtUtc,
                SubmittedByUserId = revision.SubmittedByUserId,
                SubmittedAtUtc = revision.SubmittedAtUtc,

                Decision = decision is null
                    ? null
                    : new VacancyDecisionResponse
                    {
                        DecisionId = decision.Id,

                        VacancyRevisionId =
                            decision.VacancyRevisionId,

                        ApproverUserId = decision.ApproverUserId,
                        ApproverRole = decision.ApproverRole,
                        Decision = decision.Decision,
                        Comment = decision.Comment,
                        AtUtc = decision.AtUtc
                    },

                RowVersion = Convert.ToBase64String(
                    revision.RowVersion)
            });
        }

        await transaction.CommitAsync(cancellationToken);

        return new VacancyRevisionHistoryResponse
        {
            VacancyId = vacancy.Id,
            VacancyStatus = vacancy.Status,
            CurrentRevisionId = currentRevision.Id,
            CurrentRevision = currentRevision.Revision,
            PublishedRevisionId = vacancy.PublishedRevisionId,
            TotalRevisions = items.Count,
            Revisions = items
        };
    }


    private static SubmitVacancyOperationResponse SubmitFail(
        string code,
        string message)
    {
        return new SubmitVacancyOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-068: Submit exact vacancy revision for CEO approval
    public async Task<SubmitVacancyOperationResponse> SubmitAsync(
        Guid actorUserId,
        Guid vacancyId,
        SubmitVacancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsHrAsync(actorUserId))
            return SubmitFail(
                "forbidden",
                "Access denied.");

        if (request is null ||
            vacancyId == Guid.Empty ||
            request.VacancyRevisionId == Guid.Empty)
        {
            return SubmitFail(
                "validation_error",
                "Vacancy and revision IDs are required.");
        }

        if (!TryReadRowVersion(
                request.RowVersion,
                out var vacancyRowVersion) ||
            !TryReadRowVersion(
                request.RevisionRowVersion,
                out var revisionRowVersion))
        {
            return SubmitFail(
                "validation_error",
                "Valid row versions are required.");
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            var vacancy = await _db.Vacancies
                .FirstOrDefaultAsync(
                    v => v.Id == vacancyId,
                    cancellationToken);

            if (vacancy is null)
                return SubmitFail(
                    "not_found",
                    "Vacancy not found.");

            var currentRevision = await _db.VacancyRevisions
                .Where(r => r.VacancyId == vacancyId)
                .OrderByDescending(r => r.Revision)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentRevision is null)
                return SubmitFail(
                    "invalid_state",
                    "Vacancy revision is missing.");

            if (currentRevision.Id != request.VacancyRevisionId ||
                !vacancy.RowVersion.SequenceEqual(
                    vacancyRowVersion) ||
                !currentRevision.RowVersion.SequenceEqual(
                    revisionRowVersion))
            {
                return SubmitFail(
                    "concurrency_conflict",
                    "Vacancy data changed. Reload and retry.");
            }

            if (vacancy.Status != VacancyStatuses.Draft &&
                vacancy.Status != VacancyStatuses.Paused)
            {
                return SubmitFail(
                    "invalid_state",
                    "Only draft or paused vacancies can be submitted.");
            }

            if (currentRevision.SubmittedAtUtc.HasValue ||
                currentRevision.SubmittedByUserId.HasValue)
            {
                return SubmitFail(
                    "invalid_state",
                    "This revision has already been submitted.");
            }

            if (vacancy.DepartmentId !=
                currentRevision.DepartmentId)
            {
                return SubmitFail(
                    "invalid_state",
                    "Vacancy department is inconsistent.");
            }

            var existingDecision =
                await _db.VacancyDecisions
                    .AsNoTracking()
                    .AnyAsync(
                        d => d.VacancyRevisionId ==
                            currentRevision.Id,
                        cancellationToken);

            if (existingDecision)
            {
                return SubmitFail(
                    "invalid_state",
                    "This revision already has a decision.");
            }

            var criteria = await _db.VacancyCriteria
                .AsNoTracking()
                .Where(c =>
                    c.VacancyRevisionId ==
                    currentRevision.Id)
                .ToListAsync(cancellationToken);

            var validationRequest = new CreateVacancyRequest
            {
                DepartmentId = currentRevision.DepartmentId,
                Title = currentRevision.Title,
                EmploymentType =
                    currentRevision.EmploymentType,
                Openings = currentRevision.Openings,
                Location = currentRevision.Location,
                WorkMode = currentRevision.WorkMode,
                Description = currentRevision.Description,
                MinimumExperienceYears =
                    currentRevision.MinimumExperienceYears,
                ClosingDate = currentRevision.ClosingDate,
                HiringReason = currentRevision.HiringReason,

                RequiredSkills = criteria
                    .Where(c => c.Kind ==
                        VacancyCriterionKinds.RequiredSkill)
                    .Select(c => c.Label)
                    .ToList(),

                PreferredSkills = criteria
                    .Where(c => c.Kind ==
                        VacancyCriterionKinds.PreferredSkill)
                    .Select(c => c.Label)
                    .ToList()
            };

            var validationError =
                await ValidateVacancyAsync(
                    validationRequest,
                    cancellationToken);

            if (validationError is not null)
            {
                return SubmitFail(
                    validationError.Code,
                    validationError.Message);
            }

            var now = DateTime.UtcNow;

            currentRevision.SubmittedAtUtc = now;
            currentRevision.SubmittedByUserId = actorUserId;

            vacancy.Status =
                VacancyStatuses.PendingApproval;

            vacancy.UpdatedAtUtc = now;

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "VacancySubmitted",
                EntityType = "Vacancy",
                EntityId = vacancy.Id,
                Outcome = "success",
                CorrelationId = Guid.NewGuid()
            });

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new SubmitVacancyOperationResponse
            {
                Success = true,
                Code = "vacancy_submitted",
                Message = "Vacancy submitted for CEO approval.",

                Vacancy = new SubmitVacancyResponse
                {
                    VacancyId = vacancy.Id,
                    VacancyRevisionId = currentRevision.Id,
                    Revision = currentRevision.Revision,
                    Title = currentRevision.Title,
                    Status = vacancy.Status,
                    SubmittedByUserId = actorUserId,
                    SubmittedAtUtc = now,

                    RowVersion = Convert.ToBase64String(
                        vacancy.RowVersion),

                    RevisionRowVersion =
                        Convert.ToBase64String(
                            currentRevision.RowVersion)
                }
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return SubmitFail(
                "concurrency_conflict",
                "Vacancy data changed. Reload and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            return SubmitFail(
                "concurrency_conflict",
                "Conflicting vacancy submission. Reload and retry.");
        }
        catch (SqlException ex)
            when (ex.Number == 1205)
        {
            return SubmitFail(
                "concurrency_conflict",
                "Concurrent vacancy submission. Reload and retry.");
        }
        catch (InvalidOperationException ex)
            when (IsSqlDeadlock(ex))
        {
            return SubmitFail(
                "concurrency_conflict",
                "Concurrent vacancy submission. Reload and retry.");
        }
    }

    private static DecideVacancyOperationResponse DecideFail(
        string code,
        string message)
    {
        return new DecideVacancyOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-069: CEO decision on exact submitted vacancy revision
    public async Task<DecideVacancyOperationResponse> DecideAsync(
        Guid actorUserId,
        Guid vacancyId,
        DecideVacancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCeoAsync(actorUserId))
        {
            return DecideFail(
                "forbidden",
                "Access denied.");
        }

        if (request is null ||
            vacancyId == Guid.Empty ||
            request.VacancyRevisionId == Guid.Empty)
        {
            return DecideFail(
                "validation_error",
                "Vacancy and revision IDs are required.");
        }

        if (!TryReadRowVersion(
                request.RowVersion,
                out var vacancyRowVersion) ||
            !TryReadRowVersion(
                request.RevisionRowVersion,
                out var revisionRowVersion))
        {
            return DecideFail(
                "validation_error",
                "Valid row versions are required.");
        }

        var decisionType = request.Decision?.Trim();

        if (decisionType != VacancyDecisionTypes.Approved &&
            decisionType != VacancyDecisionTypes.Rejected)
        {
            return DecideFail(
                "validation_error",
                "Decision must be Approved or Rejected.");
        }

        if (request.Comment is { Length: > 2000 })
        {
            return DecideFail(
                "validation_error",
                "Decision comment cannot exceed 2000 characters.");
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        if (decisionType == VacancyDecisionTypes.Rejected &&
            comment is null)
        {
            return DecideFail(
                "validation_error",
                "A rejection comment is required.");
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            var vacancy = await _db.Vacancies
                .FirstOrDefaultAsync(
                    v => v.Id == vacancyId,
                    cancellationToken);

            if (vacancy is null)
            {
                return DecideFail(
                    "not_found",
                    "Vacancy not found.");
            }

            var currentRevision = await _db.VacancyRevisions
                .AsNoTracking()
                .Where(r => r.VacancyId == vacancyId)
                .OrderByDescending(r => r.Revision)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentRevision is null)
            {
                return DecideFail(
                    "invalid_state",
                    "Vacancy revision is missing.");
            }

            if (currentRevision.Id != request.VacancyRevisionId ||
                !vacancy.RowVersion.SequenceEqual(
                    vacancyRowVersion) ||
                !currentRevision.RowVersion.SequenceEqual(
                    revisionRowVersion))
            {
                return DecideFail(
                    "concurrency_conflict",
                    "Vacancy data changed. Reload and retry.");
            }

            if (vacancy.Status !=
                VacancyStatuses.PendingApproval)
            {
                return DecideFail(
                    "invalid_state",
                    "Only pending vacancies can receive a CEO decision.");
            }

            if (!currentRevision.SubmittedAtUtc.HasValue ||
                !currentRevision.SubmittedByUserId.HasValue ||
                currentRevision.SubmittedByUserId == Guid.Empty)
            {
                return DecideFail(
                    "invalid_state",
                    "The current revision has not been submitted.");
            }

            if (vacancy.DepartmentId !=
                currentRevision.DepartmentId)
            {
                return DecideFail(
                    "invalid_state",
                    "Vacancy department is inconsistent.");
            }

            var existingDecision =
                await _db.VacancyDecisions
                    .AsNoTracking()
                    .AnyAsync(
                        d => d.VacancyRevisionId ==
                            currentRevision.Id,
                        cancellationToken);

            if (existingDecision)
            {
                return DecideFail(
                    "invalid_state",
                    "This revision already has a decision.");
            }

            var now = DateTime.UtcNow;

            var approved =
                decisionType == VacancyDecisionTypes.Approved;

            var decision = new VacancyDecision
            {
                VacancyRevisionId = currentRevision.Id,
                ApproverUserId = actorUserId,
                ApproverRole = AppRoles.CEO,
                Decision = decisionType!,
                Comment = comment,
                AtUtc = now
            };

            _db.VacancyDecisions.Add(decision);

            vacancy.Status = approved
                ? VacancyStatuses.Approved
                : VacancyStatuses.Rejected;

            vacancy.UpdatedAtUtc = now;

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.CEO,
                Module = "Recruitment",

                Action = approved
                    ? "VacancyApproved"
                    : "VacancyRejected",

                EntityType = "Vacancy",
                EntityId = vacancy.Id,
                Outcome = "success",
                CorrelationId = Guid.NewGuid()
            });

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new DecideVacancyOperationResponse
            {
                Success = true,

                Code = approved
                    ? "vacancy_approved"
                    : "vacancy_rejected",

                Message = approved
                    ? "Vacancy revision approved."
                    : "Vacancy revision rejected.",

                Vacancy = new DecideVacancyResponse
                {
                    VacancyId = vacancy.Id,
                    VacancyRevisionId = currentRevision.Id,
                    Revision = currentRevision.Revision,
                    Title = currentRevision.Title,
                    Status = vacancy.Status,
                    UpdatedAtUtc = now,

                    Decision = new VacancyDecisionResponse
                    {
                        DecisionId = decision.Id,
                        VacancyRevisionId =
                            decision.VacancyRevisionId,

                        ApproverUserId =
                            decision.ApproverUserId,

                        ApproverRole = decision.ApproverRole,
                        Decision = decision.Decision,
                        Comment = decision.Comment,
                        AtUtc = decision.AtUtc
                    },

                    RowVersion = Convert.ToBase64String(
                        vacancy.RowVersion),

                    RevisionRowVersion =
                        Convert.ToBase64String(
                            currentRevision.RowVersion)
                }
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return DecideFail(
                "concurrency_conflict",
                "Vacancy data changed. Reload and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            return DecideFail(
                "concurrency_conflict",
                "Conflicting vacancy decision. Reload and retry.");
        }
        catch (SqlException ex)
            when (ex.Number == 1205)
        {
            return DecideFail(
                "concurrency_conflict",
                "Concurrent vacancy decision. Reload and retry.");
        }
        catch (InvalidOperationException ex)
            when (IsSqlDeadlock(ex))
        {
            return DecideFail(
                "concurrency_conflict",
                "Concurrent vacancy decision. Reload and retry.");
        }
    }
}
