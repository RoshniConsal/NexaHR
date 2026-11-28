using NexaHR.Domain.Rules.Applications;
using NexaHR.Application.Interfaces.CvDocuments;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Applications;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Applications;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;
using NexaHR.Infrastructure.Persistence.Queries;

namespace NexaHR.Infrastructure.Services.Applications;

public sealed class ApplicationService : IApplicationService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;
    private readonly ICvFileStorage _storage;

    public ApplicationService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit,
        ICvFileStorage storage)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _storage = storage;
    }

    private static ApplicationOperationResponse Failure(
        string code,
        string message)
    {
        return new ApplicationOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private async Task<(bool EmailMatch, bool PhoneMatch)>
        DetectDuplicateContactsAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
    {
        var email = _users.NormalizeEmail(
            user.Email?.Trim());

        var phone = ContactNormalizer.NormalizePhone(
            user.PhoneNumber);

        var contacts = await (
            from other in _db.Users.AsNoTracking()
            join membership in _db.UserRoles
                on other.Id equals membership.UserId
            join role in _db.Roles
                on membership.RoleId equals role.Id
            where other.Id != user.Id &&
                  role.Name == AppRoles.Candidate
            select new
            {
                other.NormalizedEmail,
                other.PhoneNumber
            })
            .ToListAsync(cancellationToken);

        var emailMatch =
            !string.IsNullOrWhiteSpace(email) &&
            contacts.Any(c =>
                string.Equals(
                    c.NormalizedEmail,
                    email,
                    StringComparison.Ordinal));

        var phoneMatch =
            phone is not null &&
            contacts.Any(c =>
                string.Equals(
                    ContactNormalizer.NormalizePhone(
                        c.PhoneNumber),
                    phone,
                    StringComparison.Ordinal));

        return (emailMatch, phoneMatch);
    }

    private async Task<bool> VerifyCvIntegrityAsync(
        CvDocument document,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content =
                _storage.OpenCleanRead(document.StorageKey);

            if (content.Length != document.SizeBytes)
                return false;

            var hash = await SHA256.HashDataAsync(
                content,
                cancellationToken);

            return string.Equals(
                Convert.ToHexString(hash),
                document.Sha256Hash,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // API-072: Role-filtered application list.
    public async Task<ApplicationListOperationResponse> GetApplicationsAsync(
        Guid actorUserId,
        ApplicationQuery request,
        CancellationToken cancellationToken = default)
    {
        static ApplicationListOperationResponse Fail(
            string code,
            string message) => new()
        {
            Success = false,
            Code = code,
            Message = message
        };

        if (actorUserId == Guid.Empty)
            return Fail("forbidden", "Access denied.");

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return Fail(
                "forbidden",
                "Account is unavailable.");
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1)
            return Fail("forbidden", "Access denied.");

        var role = roles[0];

        var isHr = role == AppRoles.HRManager;

        if (!isHr &&
            role != AppRoles.Candidate &&
            role != AppRoles.Employee)
        {
            return Fail("forbidden", "Access denied.");
        }

        var allowedStages = new[]
        {
            ApplicationStatuses.Applied,
            ApplicationStatuses.UnderReview,
            ApplicationStatuses.Shortlisted,
            ApplicationStatuses.Interview,
            ApplicationStatuses.OfferSent,
            ApplicationStatuses.OfferAccepted,
            ApplicationStatuses.Hired,
            ApplicationStatuses.Rejected,
            ApplicationStatuses.Withdrawn,
            ApplicationStatuses.OfferDeclined
        };

        var stage = request.Stage?.Trim();

        if (!string.IsNullOrWhiteSpace(stage) &&
            !allowedStages.Contains(stage))
        {
            return Fail(
                "invalid_stage",
                "Invalid application stage.");
        }

        if (request.VacancyId == Guid.Empty)
        {
            return Fail(
                "invalid_vacancy",
                "Invalid vacancy ID.");
        }

        var applications = _db.CandidateApplications
            .AsNoTracking();

        // Candidate and Employee must see only own records.
        if (!isHr)
        {
            applications = applications.Where(
                a => a.CandidateUserId == actorUserId);
        }

        var query =
            from application in applications
            join revision in _db.VacancyRevisions.AsNoTracking()
                on new
                {
                    application.VacancyId,
                    RevisionId = application.VacancyRevisionId
                }
                equals new
                {
                    revision.VacancyId,
                    RevisionId = revision.Id
                }
            join department in _db.Departments.AsNoTracking()
                on revision.DepartmentId equals department.Id
            join candidate in _db.Users.AsNoTracking()
                on application.CandidateUserId equals candidate.Id
            select new
            {
                Application = application,
                JobTitle = revision.Title,
                DepartmentName = department.Name,
                CandidateName = candidate.FullName
            };

        if (request.VacancyId.HasValue)
        {
            var vacancyId = request.VacancyId.Value;

            query = query.Where(
                x => x.Application.VacancyId == vacancyId);
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(
                x => x.Application.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            if (isHr)
            {
                query = query.Where(x =>
                    x.JobTitle.Contains(search) ||
                    x.DepartmentName.Contains(search) ||
                    x.CandidateName.Contains(search));
            }
            else
            {
                query = query.Where(x =>
                    x.JobTitle.Contains(search) ||
                    x.DepartmentName.Contains(search));
            }
        }

        var page = Math.Clamp(
            request.Page, 1, 100000);

        var pageSize = Math.Clamp(
            request.PageSize, 1, 100);

        var totalCount = await query.CountAsync(
            cancellationToken);

        var records = await query
            .OrderByDescending(
                x => x.Application.AppliedAtUtc)
            .ThenBy(x => x.Application.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                ApplicationId = x.Application.Id,
                x.Application.VacancyId,
                x.Application.VacancyRevisionId,
                x.Application.DocumentId,
                x.JobTitle,
                x.DepartmentName,
                x.Application.Stage,
                x.Application.AppliedAtUtc,
                x.Application.UpdatedAtUtc,
                CandidateUserId = isHr
                    ? (Guid?)x.Application.CandidateUserId
                    : null,
                CandidateName = isHr
                    ? x.CandidateName
                    : null,
                RowVersion = isHr
                    ? x.Application.RowVersion
                    : null
            })
            .ToListAsync(cancellationToken);

        return new ApplicationListOperationResponse
        {
            Success = true,
            Code = "applications_retrieved",
            Message = "Applications retrieved successfully.",
            Data = new ApplicationListResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = records.Select(x =>
                    new ApplicationListItemResponse
                    {
                        ApplicationId = x.ApplicationId,
                        VacancyId = x.VacancyId,
                        VacancyRevisionId = x.VacancyRevisionId,
                        DocumentId = x.DocumentId,
                        JobTitle = x.JobTitle,
                        DepartmentName = x.DepartmentName,
                        Stage = x.Stage,
                        AppliedAtUtc = DateTime.SpecifyKind(
                            x.AppliedAtUtc, DateTimeKind.Utc),
                        UpdatedAtUtc = x.UpdatedAtUtc.HasValue
                            ? DateTime.SpecifyKind(
                                x.UpdatedAtUtc.Value,
                                DateTimeKind.Utc)
                            : null,
                        CandidateUserId = x.CandidateUserId,
                        CandidateName = x.CandidateName,
                        RowVersion = x.RowVersion is null
                            ? null
                            : Convert.ToBase64String(x.RowVersion)
                    }).ToList()
            }
        };
    }

    // API-074: Role-filtered application details.
    public async Task<ApplicationDetailsOperationResponse>
        GetApplicationDetailsAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default)
    {
        static ApplicationDetailsOperationResponse Fail(
            string code,
            string message) => new()
        {
            Success = false,
            Code = code,
            Message = message
        };

        if (actorUserId == Guid.Empty)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        if (applicationId == Guid.Empty)
        {
            return Fail(
                "invalid_application",
                "Invalid application ID.");
        }

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return Fail(
                "forbidden",
                "Account is unavailable.");
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        var role = roles[0];

        var isHr = role == AppRoles.HRManager;

        if (!isHr &&
            role != AppRoles.Candidate &&
            role != AppRoles.Employee)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

        var applications = _db.CandidateApplications
            .AsNoTracking()
            .Where(a => a.Id == applicationId);

        if (!isHr)
        {
            applications = applications.Where(
                a => a.CandidateUserId == actorUserId);
        }

        var record = await (
            from application in applications

            join revision in
                _db.VacancyRevisions.AsNoTracking()

                on new
                {
                    application.VacancyId,
                    RevisionId = application.VacancyRevisionId
                }

                equals new
                {
                    revision.VacancyId,
                    RevisionId = revision.Id
                }

            join department in
                _db.Departments.AsNoTracking()

                on revision.DepartmentId equals department.Id

            join candidate in
                _db.Users.AsNoTracking()

                on application.CandidateUserId equals candidate.Id

            select new
            {
                ApplicationId = application.Id,
                application.VacancyId,
                application.VacancyRevisionId,
                application.DocumentId,
                JobTitle = revision.Title,
                DepartmentName = department.Name,
                application.Stage,
                application.AppliedAtUtc,
                application.UpdatedAtUtc,

                CandidateUserId = isHr
                    ? (Guid?)application.CandidateUserId
                    : null,

                CandidateName = isHr
                    ? candidate.FullName
                    : null,

                RowVersion = isHr || role == AppRoles.Candidate
                    ? application.RowVersion
                    : null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return Fail(
                "application_not_found",
                "Application not found.");
        }

        IReadOnlyList<ApplicationAiConsentResponse>?
            aiConsents = null;

        // Consent information is available only to the owner.
        if (!isHr)
        {
            var events = await _db.ApplicationConsents
                .AsNoTracking()
                .Where(c =>
                    c.ApplicationId == applicationId &&
                    c.ActorUserId == actorUserId &&
                    c.Purpose ==
                        ApplicationConsentPurposes.AiProcessing &&
                    c.Provider != null &&
                    c.ProcessingPolicyId.HasValue)
                .OrderByDescending(c => c.AtUtc)
                .ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.Provider,
                    c.ProcessingPolicyId,
                    c.TextVersion,
                    c.Granted,
                    c.AtUtc
                })
                .ToListAsync(cancellationToken);

            aiConsents = events.Select(c =>
                new ApplicationAiConsentResponse
                {
                    ConsentId = c.Id,
                    Provider = c.Provider!,
                    ProcessingPolicyId =
                        c.ProcessingPolicyId!.Value,
                    TextVersion = c.TextVersion,
                    Granted = c.Granted,
                    AtUtc = DateTime.SpecifyKind(
                        c.AtUtc,
                        DateTimeKind.Utc)
                }).ToList();
        }


        ApplicationAiConsentPolicyResponse?
            aiConsentPolicy = null;

        // Only the owner Candidate receives the
        // current consent presentation.
        if (role == AppRoles.Candidate)
        {
            var policy = await _db.ProcessingPolicies
                .AsNoTracking()
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy is null)
            {
                aiConsentPolicy =
                    new ApplicationAiConsentPolicyResponse
                    {
                        IsConfigured = false,
                        CanGrant = false,
                        CanRevoke = false,
                        Granted = false,
                        RealDataProcessingEnabled = false
                    };
            }
            else
            {
                var consentText = BuildAiConsentText(policy);

                var textVersion =
                    GetAiConsentTextVersion(policy);

                var latestConsent = aiConsents?
                    .FirstOrDefault(c =>
                        c.ProcessingPolicyId == policy.Id &&
                        string.Equals(
                            c.Provider,
                            policy.ProviderName,
                            StringComparison.Ordinal));

                var policyValid =
                    policy.Id != Guid.Empty &&
                    policy.Version > 0 &&
                    policy.ApprovedByUserId != Guid.Empty &&
                    policy.ApprovedAtUtc <= DateTime.UtcNow &&
                    !string.IsNullOrWhiteSpace(
                        policy.ApprovalReference) &&
                    !string.IsNullOrWhiteSpace(
                        policy.ProviderName) &&
                    policy.ProviderName.Length <= 200 &&
                    !policy.ProviderName.Any(char.IsControl) &&
                    !string.IsNullOrWhiteSpace(consentText) &&
                    consentText.Length <= 10000;

                var activeRecruitment =
                    record.Stage is (
                        ApplicationStatuses.Applied or
                        ApplicationStatuses.UnderReview or
                        ApplicationStatuses.Shortlisted or
                        ApplicationStatuses.Interview or
                        ApplicationStatuses.OfferSent or
                        ApplicationStatuses.OfferAccepted);

                aiConsentPolicy =
                    new ApplicationAiConsentPolicyResponse
                    {
                        IsConfigured = true,

                        CanGrant =
                            policyValid &&
                            activeRecruitment,

                        CanRevoke =
                            latestConsent?.Granted == true,

                        // Recording consent never enables
                        // production AI processing.
                        RealDataProcessingEnabled = false,

                        ProcessingPolicyId = policy.Id,
                        PolicyVersion = policy.Version,
                        Provider = policy.ProviderName,

                        TextVersion = textVersion,

                        ConsentText = policyValid
                            ? consentText
                            : null,

                        Granted =
                            latestConsent?.Granted == true,

                        LatestConsentId =
                            latestConsent?.ConsentId,

                        LatestConsentAtUtc =
                            latestConsent?.AtUtc
                    };
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new ApplicationDetailsOperationResponse
        {
            Success = true,
            Code = "application_retrieved",
            Message = "Application retrieved successfully.",

            Data = new ApplicationDetailsResponse
            {
                ApplicationId = record.ApplicationId,
                VacancyId = record.VacancyId,
                VacancyRevisionId = record.VacancyRevisionId,
                DocumentId = record.DocumentId,
                JobTitle = record.JobTitle,
                DepartmentName = record.DepartmentName,
                Stage = record.Stage,

                AppliedAtUtc = DateTime.SpecifyKind(
                    record.AppliedAtUtc,
                    DateTimeKind.Utc),

                UpdatedAtUtc =
                    record.UpdatedAtUtc.HasValue
                        ? DateTime.SpecifyKind(
                            record.UpdatedAtUtc.Value,
                            DateTimeKind.Utc)
                        : null,

                CandidateUserId = record.CandidateUserId,
                CandidateName = record.CandidateName,

                RowVersion = record.RowVersion is null
                    ? null
                    : Convert.ToBase64String(
                        record.RowVersion),

                AiConsents = aiConsents,
                AiConsentPolicy = aiConsentPolicy
            }
        };
    }

    // API-075: Role-filtered application timeline.
    public async Task<ApplicationTimelineOperationResponse>
        GetApplicationTimelineAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default)
    {
        static ApplicationTimelineOperationResponse Fail(
            string code,
            string message) => new()
        {
            Success = false,
            Code = code,
            Message = message
        };

        if (actorUserId == Guid.Empty)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        if (applicationId == Guid.Empty)
        {
            return Fail(
                "invalid_application",
                "Invalid application ID.");
        }

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return Fail(
                "forbidden",
                "Account is unavailable.");
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        var role = roles[0];

        var isHr = role == AppRoles.HRManager;

        if (!isHr &&
            role != AppRoles.Candidate &&
            role != AppRoles.Employee)
        {
            return Fail(
                "forbidden",
                "Access denied.");
        }

        // Keep the current stage and timeline read consistent.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var applications = _db.CandidateApplications
            .AsNoTracking()
            .Where(a => a.Id == applicationId);

        if (!isHr)
        {
            applications = applications.Where(
                a => a.CandidateUserId == actorUserId);
        }

        var application = await applications
            .Select(a => new
            {
                a.Id,
                a.Stage
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (application is null)
        {
            return Fail(
                "application_not_found",
                "Application not found.");
        }

        var records = await _db.ApplicationStageEvents
            .AsNoTracking()
            .Where(e =>
                e.ApplicationId == application.Id)
            .OrderBy(e => e.AtUtc)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.FromStage,
                e.ToStage,
                e.AtUtc,

                ActorUserId = isHr
                    ? (Guid?)e.ActorUserId
                    : null,

                ActorRole = isHr
                    ? e.ActorRole
                    : null,

                Reason = isHr
                    ? e.Reason
                    : null
            })
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return new ApplicationTimelineOperationResponse
        {
            Success = true,
            Code = "application_timeline_retrieved",
            Message = "Application timeline retrieved.",

            Data = new ApplicationTimelineResponse
            {
                ApplicationId = application.Id,

                CurrentStage = application.Stage,

                Events = records.Select(e =>
                    new ApplicationTimelineEventResponse
                    {
                        EventId = e.Id,
                        FromStage = e.FromStage,
                        ToStage = e.ToStage,

                        AtUtc = DateTime.SpecifyKind(
                            e.AtUtc,
                            DateTimeKind.Utc),

                        ActorUserId = e.ActorUserId,
                        ActorRole = e.ActorRole,
                        Reason = e.Reason
                    }).ToList()
            }
        };
    }

    // API-076: Application stage transitions.
    public async Task<ApplicationStageTransitionOperationResponse>
        TransitionStageAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationStageTransitionRequest request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return await TransitionStageCoreAsync(
                actorUserId,
                applicationId,
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _db.ChangeTracker.Clear();
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (Exception ex) when (HasSqlConflict(ex))
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_database_error",
                "Unable to complete the stage transition.");
        }
        catch (System.Data.Common.DbException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_database_error",
                "Unable to complete the stage transition.");
        }
        catch (System.Text.Json.JsonException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_source_invalid",
                "Application review data is unavailable.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_source_invalid",
                "Application transition could not be completed.");
        }
        catch (ArgumentException)
        {
            _db.ChangeTracker.Clear();

            return TransitionFailure(
                "application_source_invalid",
                "Application transition could not be completed.");
        }
    }

    private static ApplicationStageTransitionOperationResponse
        TransitionFailure(string code, string message)
    {
        return new ApplicationStageTransitionOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private async Task<ApplicationStageTransitionOperationResponse>
        TransitionStageCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationStageTransitionRequest request,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Authorize before looking up the application.
        var actor = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
        {
            return TransitionFailure(
                "forbidden",
                "Access denied.");
        }

        var roles = await _users.GetRolesAsync(actor);

        if (roles.Count != 1 ||
            roles[0] != AppRoles.HRManager)
        {
            return TransitionFailure(
                "forbidden",
                "Access denied.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            !ApplicationStageTransitionRules.IsSupportedTarget(
                request.TargetStage))
        {
            return TransitionFailure(
                "invalid_stage_transition_request",
                "A valid application and target stage are required.");
        }

        var reason = request.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
            reason = null;

        if ((request.Reason?.Length ?? 0) > 2000 ||
            (reason is not null &&
             reason.Any(char.IsControl)))
        {
            return TransitionFailure(
                "invalid_stage_transition_request",
                "The transition reason is invalid.");
        }

        if (ApplicationStageTransitionRules.RequiresReason(
                request.TargetStage) &&
            reason is null)
        {
            return TransitionFailure(
                "stage_transition_reason_required",
                "A rejection reason is required.");
        }

        var expectedVersion = new byte[8];

        if (request.RowVersion is null ||
            request.RowVersion.Length != 12 ||
            !Convert.TryFromBase64String(
                request.RowVersion,
                expectedVersion,
                out var bytesWritten) ||
            bytesWritten != 8 ||
            Convert.ToBase64String(expectedVersion) !=
                request.RowVersion)
        {
            return TransitionFailure(
                "invalid_stage_transition_request",
                "A valid application row version is required.");
        }

        var application = await _db.CandidateApplications
            .SingleOrDefaultAsync(
                a => a.Id == applicationId,
                cancellationToken);

        if (application is null)
        {
            return TransitionFailure(
                "application_not_found",
                "Application not found.");
        }

        if (!application.RowVersion.SequenceEqual(
                expectedVersion))
        {
            return TransitionFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }

        var previousStage = application.Stage;
        var targetStage = request.TargetStage;

        if (!ApplicationStageTransitionRules.IsAllowed(
                previousStage,
                targetStage))
        {
            return TransitionFailure(
                "invalid_stage_transition",
                "The requested stage transition is not allowed.");
        }

        if (ApplicationStageTransitionRules
            .RequiresConfirmedCvReview(
                previousStage,
                targetStage))
        {
            var review = await _db.Set<CvReviewVersion>()
                .AsNoTracking()
                .Where(r =>
                    r.ApplicationId == application.Id &&
                    r.DocumentId == application.DocumentId)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (review is null ||
                review.Status != "Confirmed" ||
                !review.ConfirmedAtUtc.HasValue ||
                review.ConfirmedAtUtc.Value <
                    review.CreatedAtUtc)
            {
                return TransitionFailure(
                    "confirmed_cv_review_required",
                    "Confirm the latest CV review before continuing.");
            }

            var parent = await _db.Set<CvReviewVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(r =>
                    r.Id == review.PreviousReviewVersionId &&
                    r.ApplicationId == application.Id &&
                    r.DocumentId == application.DocumentId,
                    cancellationToken);

            if (parent is null ||
                parent.Status != "Draft" ||
                parent.Version != review.Version - 1 ||
                parent.VacancyRevisionId !=
                    review.VacancyRevisionId ||
                parent.ExtractionRunId !=
                    review.ExtractionRunId ||
                parent.CreatedAtUtc > review.CreatedAtUtc ||
                parent.ValuesJson != review.ValuesJson ||
                parent.DuplicateResolutionJson !=
                    review.DuplicateResolutionJson ||
                review.ChangedFieldsJson != "[]")
            {
                return TransitionFailure(
                    "confirmed_cv_review_required",
                    "The latest CV review requires confirmation.");
            }

            // Both the original application criteria and the
            // currently confirmed review criteria must be approved.
            var revisionIds = new[]
            {
                application.VacancyRevisionId,
                review.VacancyRevisionId
            }.Distinct().ToArray();

            foreach (var revisionId in revisionIds)
            {
                var revision = await _db.Set<VacancyRevision>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(r =>
                        r.Id == revisionId &&
                        r.VacancyId == application.VacancyId,
                        cancellationToken);

                var decisions = await _db.Set<VacancyDecision>()
                    .AsNoTracking()
                    .Where(d =>
                        d.VacancyRevisionId == revisionId)
                    .ToListAsync(cancellationToken);

                if (revision is null ||
                    !revision.SubmittedAtUtc.HasValue ||
                    !revision.SubmittedByUserId.HasValue ||
                    decisions.Count != 1 ||
                    decisions[0].ApproverRole != AppRoles.CEO ||
                    decisions[0].Decision !=
                        VacancyDecisionTypes.Approved ||
                    decisions[0].AtUtc <
                        revision.SubmittedAtUtc.Value)
                {
                    return TransitionFailure(
                        "cv_review_revision_not_approved",
                        "Approved vacancy criteria are required.");
                }
            }

            var extraction = await _db.Set<CvExtractionRun>()
                .AsNoTracking()
                .Where(e =>
                    e.ApplicationId == application.Id &&
                    e.DocumentId == application.DocumentId)
                .OrderByDescending(e => e.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (extraction?.Id != review.ExtractionRunId ||
                (extraction is not null &&
                 (extraction.Status is not
                    ("ReadyForReview" or "Failed") ||
                  !extraction.CompletedAtUtc.HasValue ||
                  extraction.CompletedAtUtc.Value >
                      review.CreatedAtUtc)))
            {
                return TransitionFailure(
                    "confirmed_cv_review_required",
                    "Confirm the review for the latest extraction.");
            }

            var document = await _db.Set<CvDocument>()
                .AsNoTracking()
                .SingleOrDefaultAsync(d =>
                    d.Id == application.DocumentId &&
                    d.OwnerUserId == application.CandidateUserId,
                    cancellationToken);

            if (document is null ||
                document.ScanStatus != "Clean" ||
                !document.ScannedAtUtc.HasValue ||
                !await VerifyCvIntegrityAsync(
                    document, cancellationToken))
            {
                return TransitionFailure(
                    "cv_review_document_not_available",
                    "The selected CV is unavailable.");
            }

            // Confirmed review data must contain mandatory values.
            using var values =
                System.Text.Json.JsonDocument.Parse(
                    review.ValuesJson);

            var root = values.RootElement;

            if (root.ValueKind !=
                    System.Text.Json.JsonValueKind.Object ||
                !root.TryGetProperty("fullName", out var name) ||
                name.ValueKind !=
                    System.Text.Json.JsonValueKind.String ||
                string.IsNullOrWhiteSpace(name.GetString()) ||
                !root.TryGetProperty("email", out var email) ||
                email.ValueKind !=
                    System.Text.Json.JsonValueKind.String ||
                string.IsNullOrWhiteSpace(email.GetString()))
            {
                return TransitionFailure(
                    "confirmed_cv_review_required",
                    "The confirmed CV review is incomplete.");
            }
        }

        var now = DateTime.UtcNow;

        _db.Entry(application)
            .Property(a => a.RowVersion)
            .OriginalValue = expectedVersion;

        application.Stage = targetStage;
        application.UpdatedAtUtc = now;

        var stageEvent = new ApplicationStageEvent
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            ActorUserId = actorUserId,
            ActorRole = AppRoles.HRManager,
            FromStage = previousStage,
            ToStage = targetStage,
            Reason = reason,
            AtUtc = now
        };

        _db.ApplicationStageEvents.Add(stageEvent);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = AppRoles.HRManager,
            Module = "Recruitment",
            Action = "ApplicationStageChanged",
            EntityType = "CandidateApplication",
            EntityId = application.Id,
            Outcome = "success"
        });

        // Never expose internal rejection reasons in notifications.
        _db.Notifications.Add(new Notification
        {
            RecipientUserId = application.CandidateUserId,
            Type = "ApplicationStageChanged",
            Title = "Application status updated",
            Message = "Your job application status has been updated.",
            TargetType = null,
            TargetId = null,
            IsRead = false,
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(expectedVersion))
        {
            throw new InvalidOperationException(
                "Application row version was not advanced.");
        }

        await transaction.CommitAsync(CancellationToken.None);

        return new ApplicationStageTransitionOperationResponse
        {
            Success = true,
            Code = "application_stage_changed",
            Message = "Application stage updated successfully.",
            Data = new ApplicationStageTransitionResponse
            {
                ApplicationId = application.Id,
                EventId = stageEvent.Id,
                PreviousStage = previousStage,
                CurrentStage = targetStage,
                AtUtc = now,
                RowVersion = Convert.ToBase64String(
                    application.RowVersion)
            }
        };
    }


    // API-078: Candidate-owned provider-specific AI consent.
    public async Task<ApplicationAiConsentOperationResponse>
        UpdateAiConsentAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationAiConsentRequest request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return await UpdateAiConsentCoreAsync(
                actorUserId,
                applicationId,
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _db.ChangeTracker.Clear();
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return AiConsentFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (Exception ex) when (HasSqlConflict(ex))
        {
            _db.ChangeTracker.Clear();

            return AiConsentFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return AiConsentFailure(
                "application_database_error",
                "Unable to update AI consent.");
        }
        catch (System.Data.Common.DbException)
        {
            _db.ChangeTracker.Clear();

            return AiConsentFailure(
                "application_database_error",
                "Unable to update AI consent.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();

            return AiConsentFailure(
                "application_source_invalid",
                "Unable to verify consent information.");
        }
    }

    private static ApplicationAiConsentOperationResponse
        AiConsentFailure(
            string code,
            string message)
    {
        return new ApplicationAiConsentOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private static ApplicationAiConsentOperationResponse
        AiConsentSuccess(
            CandidateApplication application,
            ApplicationConsent consent,
            bool changed)
    {
        return new ApplicationAiConsentOperationResponse
        {
            Success = true,
            Code = changed
                ? "ai_consent_updated"
                : "ai_consent_unchanged",

            Message = changed
                ? "AI consent updated successfully."
                : "AI consent is already in the requested state.",

            Data = new ApplicationAiConsentUpdateResponse
            {
                ApplicationId = application.Id,
                Stage = application.Stage,
                RowVersion = Convert.ToBase64String(
                    application.RowVersion),

                Changed = changed,

                Consent = new ApplicationAiConsentResponse
                {
                    ConsentId = consent.Id,
                    Provider = consent.Provider!,
                    ProcessingPolicyId =
                        consent.ProcessingPolicyId!.Value,

                    TextVersion = consent.TextVersion,
                    Granted = consent.Granted,
                    AtUtc = DateTime.SpecifyKind(
                        consent.AtUtc,
                        DateTimeKind.Utc)
                }
            }
        };
    }

    private static string GetAiConsentTextVersion(
        ProcessingPolicy policy)
    {
        return "ai-consent-v1-policy-" +
            policy.Version.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildAiConsentText(
        ProcessingPolicy policy)
    {
        return
            $"Optional AI processing provider: {policy.ProviderName}. " +
            $"Purpose: {policy.ProcessingPurpose}. " +
            $"Data categories: {policy.DataCategories}. " +
            $"Provider retention: {policy.ProviderRetention}. " +
            $"Processing location: {policy.ProcessingLocation}. " +
            $"Provider access: {policy.ProviderAccessPolicy}. " +
            $"Provider training allowed: " +
            $"{(policy.ProviderTrainingAllowed ? "Yes" : "No")}. " +
            "AI consent is optional. " +
            "Manual recruitment remains available without AI consent. " +
            "You may revoke permission for future AI processing. " +
            "Consent does not itself enable production AI processing.";
    }

    private async Task<ApplicationAiConsentOperationResponse>
        UpdateAiConsentCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationAiConsentRequest request,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

        // Authorization before application lookup.
        var actor = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
        {
            return AiConsentFailure(
                "forbidden",
                "Access denied.");
        }

        var roles = await _users.GetRolesAsync(actor);

        if (roles.Count != 1 ||
            roles[0] != AppRoles.Candidate)
        {
            return AiConsentFailure(
                "forbidden",
                "Candidate access is required.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            request.ProcessingPolicyId is not Guid policyId ||
            policyId == Guid.Empty ||
            !request.Granted.HasValue ||
            string.IsNullOrWhiteSpace(request.Provider) ||
            request.Provider.Length > 200 ||
            request.Provider.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.TextVersion) ||
            request.TextVersion.Length > 100 ||
            request.TextVersion.Any(char.IsControl))
        {
            return AiConsentFailure(
                "invalid_ai_consent_request",
                "Invalid AI consent request.");
        }

        var provider = request.Provider.Trim();
        var textVersion = request.TextVersion.Trim();

        if (string.IsNullOrWhiteSpace(request.RowVersion) ||
            request.RowVersion.Length != 12)
        {
            return AiConsentFailure(
                "invalid_ai_consent_request",
                "Invalid application concurrency token.");
        }

        var expectedToken = new byte[8];

        if (!Convert.TryFromBase64String(
                request.RowVersion,
                expectedToken,
                out var bytesWritten) ||
            bytesWritten != 8 ||
            Convert.ToBase64String(expectedToken) !=
                request.RowVersion)
        {
            return AiConsentFailure(
                "invalid_ai_consent_request",
                "Invalid application concurrency token.");
        }

        // Ownership filter also prevents application enumeration.
        var application = await _db.CandidateApplications
            .AsTracking()
            .SingleOrDefaultAsync(
                a => a.Id == applicationId &&
                     a.CandidateUserId == actorUserId,
                cancellationToken);

        if (application is null)
        {
            return AiConsentFailure(
                "application_not_found",
                "Application not found.");
        }

        if (!application.RowVersion.SequenceEqual(expectedToken))
        {
            return AiConsentFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }

        var policy = await _db.ProcessingPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.Id == policyId,
                cancellationToken);

        if (policy is null ||
            !string.Equals(
                policy.ProviderName,
                provider,
                StringComparison.Ordinal))
        {
            return AiConsentFailure(
                "ai_consent_policy_invalid",
                "Processing policy or provider is invalid.");
        }

        var previous = await _db.ApplicationConsents
            .AsNoTracking()
            .Where(c =>
                c.ApplicationId == applicationId &&
                c.ActorUserId == actorUserId &&
                c.Purpose ==
                    ApplicationConsentPurposes.AiProcessing &&
                c.ProcessingPolicyId == policyId &&
                c.Provider == provider)
            .OrderByDescending(c => c.AtUtc)
            .ThenBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        string consentText;

        if (request.Granted.Value)
        {
            // Grants require the latest recorded policy.
            var latestPolicy = await _db.ProcessingPolicies
                .AsNoTracking()
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestPolicy is null ||
                latestPolicy.Id != policyId)
            {
                return AiConsentFailure(
                    "ai_consent_policy_conflict",
                    "Reload the current processing policy.");
            }

            if (policy.ApprovedByUserId == Guid.Empty ||
                policy.ApprovedAtUtc > now ||
                string.IsNullOrWhiteSpace(
                    policy.ApprovalReference))
            {
                return AiConsentFailure(
                    "ai_consent_policy_invalid",
                    "Processing policy is unavailable.");
            }

            // New grants are limited to active recruitment.
            if (application.Stage is not (
                ApplicationStatuses.Applied or
                ApplicationStatuses.UnderReview or
                ApplicationStatuses.Shortlisted or
                ApplicationStatuses.Interview or
                ApplicationStatuses.OfferSent or
                ApplicationStatuses.OfferAccepted))
            {
                return AiConsentFailure(
                    "ai_consent_stage_conflict",
                    "New AI consent is unavailable for this application.");
            }

            if (textVersion != GetAiConsentTextVersion(policy))
            {
                return AiConsentFailure(
                    "ai_consent_text_conflict",
                    "Reload the current consent information.");
            }

            consentText = BuildAiConsentText(policy);
        }
        else
        {
            // Historical permissions remain revocable.
            if (previous is null)
            {
                return AiConsentFailure(
                    "ai_consent_not_found",
                    "No existing AI permission was found.");
            }

            if (previous.TextVersion != textVersion)
            {
                return AiConsentFailure(
                    "ai_consent_text_conflict",
                    "Reload the existing consent information.");
            }

            consentText = previous.ConsentText;
        }

        if (string.IsNullOrWhiteSpace(consentText) ||
            consentText.Length > 10000)
        {
            return AiConsentFailure(
                "application_source_invalid",
                "Consent information is unavailable.");
        }

        // Same desired state with a current token is idempotent.
        if (previous is not null &&
            previous.Granted == request.Granted.Value &&
            previous.TextVersion == textVersion &&
            previous.ConsentText == consentText)
        {
            await transaction.CommitAsync(
                cancellationToken);

            return AiConsentSuccess(
                application,
                previous,
                false);
        }

        // Preserve deterministic chronological ordering.
        if (previous is not null &&
            now <= previous.AtUtc)
        {
            now = previous.AtUtc.AddTicks(1);
        }

        var consent = new ApplicationConsent
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            ActorUserId = actorUserId,
            Purpose = ApplicationConsentPurposes.AiProcessing,
            Provider = provider,
            ProcessingPolicyId = policyId,
            TextVersion = textVersion,
            ConsentText = consentText,
            Granted = request.Granted.Value,
            AtUtc = now
        };

        _db.ApplicationConsents.Add(consent);

        var entry = _db.Entry(application);

        entry.Property(a => a.RowVersion)
            .OriginalValue = expectedToken;

        application.UpdatedAtUtc = now;

        entry.Property(a => a.UpdatedAtUtc)
            .IsModified = true;

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = AppRoles.Candidate,
            Module = "Recruitment",
            Action = "ApplicationAiConsentChanged",
            EntityType = "ApplicationConsent",
            EntityId = consent.Id,
            Outcome = "success"
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(expectedToken))
        {
            throw new InvalidOperationException(
                "Application concurrency token did not advance.");
        }

        var result = AiConsentSuccess(
            application,
            consent,
            true);

        await transaction.CommitAsync(
            CancellationToken.None);

        return result;
    }

    // API-073: Submit candidate application.
    public async Task<ApplicationOperationResponse> SubmitAsync(
        Guid actorUserId,
        SubmitApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await SubmitCoreAsync(
                actorUserId,
                request,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
            when (HasSqlConflict(ex))
        {
            _db.ChangeTracker.Clear();

            return Failure(
                "application_conflict",
                "Application conflict. Refresh and check your applications.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            _db.ChangeTracker.Clear();

            return Failure(
                "application_conflict",
                "Application conflict. Refresh and check your applications.");
        }
        catch (SqlException ex)
            when (ex.Number is 2601 or 2627 or 1205)
        {
            _db.ChangeTracker.Clear();

            return Failure(
                "application_conflict",
                "Application conflict. Refresh and check your applications.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return Failure(
                "application_database_error",
                "Unable to complete the application. Check your applications before retrying.");
        }
        catch (SqlException)
        {
            _db.ChangeTracker.Clear();

            return Failure(
                "application_database_error",
                "Unable to confirm the application result. Check your applications before retrying.");
        }
    }

    private static bool HasSqlConflict(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is SqlException sql &&
                sql.Number is 2601 or 2627 or 1205)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ApplicationOperationResponse> SubmitCoreAsync(
        Guid actorUserId,
        SubmitApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
            return Failure(
                "forbidden",
                "Candidate access is required.");

        if (request.VacancyId is not Guid vacancyId ||
            vacancyId == Guid.Empty ||
            request.VacancyRevisionId is not Guid revisionId ||
            revisionId == Guid.Empty ||
            request.DocumentId is not Guid documentId ||
            documentId == Guid.Empty)
        {
            return Failure(
                "invalid_request",
                "Vacancy, revision and CV are required.");
        }

        if (request.ConsentGranted != true ||
            !string.Equals(
                request.ConsentTextVersion,
                SubmissionConsent.Version,
                StringComparison.Ordinal))
        {
            return Failure(
                "submission_consent_required",
                "Current submission consent is required.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return Failure(
                "forbidden",
                "Candidate account is unavailable.");
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1 ||
            roles[0] != AppRoles.Candidate)
        {
            return Failure(
                "forbidden",
                "Candidate access is required.");
        }

        if (string.IsNullOrWhiteSpace(user.FullName) ||
            user.FullName.Trim().Length < 2 ||
            string.IsNullOrWhiteSpace(user.Email) ||
            !new EmailAddressAttribute().IsValid(user.Email) ||
            string.IsNullOrWhiteSpace(user.PhoneNumber) ||
            !new PhoneAttribute().IsValid(user.PhoneNumber) ||
            ContactNormalizer.NormalizePhone(
                user.PhoneNumber) is null)
        {
            return Failure(
                "profile_incomplete",
                "Complete your contact details before applying.");
        }

        var job = await PublishedVacancyQuery.Query(_db)
            .SingleOrDefaultAsync(
                x => x.Vacancy.Id == vacancyId &&
                     x.Revision.Id == revisionId,
                cancellationToken);

        if (job is null)
        {
            return Failure(
                "vacancy_unavailable",
                "The selected vacancy revision is unavailable.");
        }

        var document = await _db.CvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                d => d.Id == documentId &&
                     d.OwnerUserId == actorUserId &&
                     d.ScanStatus == CvScanStatuses.Clean &&
                     d.ScannedAtUtc.HasValue,
                cancellationToken);

        if (document is null)
        {
            return Failure(
                "cv_unavailable",
                "An eligible CV document is required.");
        }

        var alreadyApplied =
            await _db.CandidateApplications.AnyAsync(
                a => a.CandidateUserId == actorUserId &&
                     a.VacancyId == vacancyId,
                cancellationToken);

        if (alreadyApplied)
        {
            return Failure(
                "duplicate_application",
                "You have already applied for this vacancy.");
        }

        if (!await VerifyCvIntegrityAsync(
            document,
            cancellationToken))
        {
            return Failure(
                "cv_integrity_failed",
                "The selected CV is unavailable or failed integrity verification.");
        }

        var now = DateTime.UtcNow;

        var profileExists =
            await _db.CandidateProfiles.AnyAsync(
                p => p.UserId == actorUserId,
                cancellationToken);

        if (!profileExists)
        {
            _db.CandidateProfiles.Add(
                new CandidateProfile
                {
                    UserId = actorUserId,
                    CreatedAtUtc = now
                });
        }

        var duplicateContacts =
            await DetectDuplicateContactsAsync(
                user,
                cancellationToken);

        var application = new CandidateApplication
        {
            CandidateUserId = actorUserId,
            VacancyId = vacancyId,
            VacancyRevisionId = revisionId,
            DocumentId = documentId,
            Stage = ApplicationStatuses.Applied,
            DuplicateEmailFlag = duplicateContacts.EmailMatch,
            DuplicatePhoneFlag = duplicateContacts.PhoneMatch,
            AppliedAtUtc = now
        };

        _db.CandidateApplications.Add(application);

        _db.ApplicationConsents.Add(
            new ApplicationConsent
            {
                ApplicationId = application.Id,
                ActorUserId = actorUserId,
                Purpose = ApplicationConsentPurposes.Submission,
                TextVersion = SubmissionConsent.Version,
                ConsentText = SubmissionConsent.Text,
                Granted = true,
                AtUtc = now
            });

        _db.ApplicationStageEvents.Add(
            new ApplicationStageEvent
            {
                ApplicationId = application.Id,
                ActorUserId = actorUserId,
                ActorRole = AppRoles.Candidate,
                FromStage = null,
                ToStage = ApplicationStatuses.Applied,
                AtUtc = now
            });

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = AppRoles.Candidate,
            Module = "Recruitment",
            Action = "ApplicationSubmitted",
            EntityType = "CandidateApplication",
            EntityId = application.Id,
            Outcome = "success"
        });

        _db.Notifications.Add(
            new Notification
            {
                RecipientUserId = actorUserId,
                Type = "ApplicationSubmitted",
                Title = "Application submitted",
                Message = "Your job application was submitted successfully.",
                TargetType = null,
                TargetId = null,
                IsRead = false,
                CreatedAtUtc = now
            });

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(CancellationToken.None);

        return new ApplicationOperationResponse
        {
            Success = true,
            Code = "application_submitted",
            Message = "Application submitted successfully.",
            Application = new ApplicationResponse
            {
                ApplicationId = application.Id,
                VacancyId = application.VacancyId,
                VacancyRevisionId = application.VacancyRevisionId,
                DocumentId = application.DocumentId,
                JobTitle = job.Revision.Title,
                Stage = application.Stage,
                AppliedAtUtc = application.AppliedAtUtc,
                UpdatedAtUtc = application.UpdatedAtUtc,
                RowVersion = Convert.ToBase64String(
                    application.RowVersion)
            }
        };
    }
}
