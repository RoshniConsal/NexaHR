using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvExtractions;
using NexaHR.Application.Interfaces.CvExtractions;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed class CvExtractionService : ICvExtractionService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public CvExtractionService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private async Task<bool> IsAuthorizedHrAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
            return false;

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return false;

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
            roles[0] == AppRoles.HRManager;
    }

    private static CvExtractionOperationResponse<T>
        Fail<T>(string code, string message)
        where T : class
        => new(false, code, message, null);

    private static CvExtractionOperationResponse<T>
        Ok<T>(string code, string message, T data)
        where T : class
        => new(true, code, message, data);

    // API-084
    public async Task<
        CvExtractionOperationResponse<CvExtractionResponse>>
        StartAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await IsAuthorizedHrAsync(
                    actorUserId, cancellationToken))
                return Fail<CvExtractionResponse>(
                    "forbidden",
                    "HR Manager access is required.");

            if (applicationId == Guid.Empty)
                return Fail<CvExtractionResponse>(
                    "invalid_application",
                    "A valid application ID is required.");

            var application = await _db.CandidateApplications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    a => a.Id == applicationId,
                    cancellationToken);

            if (application is null)
                return Fail<CvExtractionResponse>(
                    "application_not_found",
                    "Application was not found.");

            var document = await _db.CvDocuments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    d => d.Id == application.DocumentId &&
                         d.OwnerUserId ==
                             application.CandidateUserId,
                    cancellationToken);

            if (document is null ||
                document.ScanStatus != CvScanStatuses.Clean ||
                !document.ScannedAtUtc.HasValue)
                return Fail<CvExtractionResponse>(
                    "document_not_available",
                    "A clean submitted CV is required.");

            if (document.Sha256Hash.Length != 64 ||
                !document.Sha256Hash.All(Uri.IsHexDigit))
                return Fail<CvExtractionResponse>(
                    "source_hash_invalid",
                    "The CV source hash is invalid.");

            var revision = await _db.VacancyRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    r => r.Id == application.VacancyRevisionId &&
                         r.VacancyId == application.VacancyId,
                    cancellationToken);

            var decisions = await _db.Set<VacancyDecision>()
                .AsNoTracking()
                .Where(d =>
                    d.VacancyRevisionId ==
                        application.VacancyRevisionId)
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
                return Fail<CvExtractionResponse>(
                    "revision_not_approved",
                    "An approved vacancy revision is required.");

            var latest = await _db.CvExtractionRuns
                .AsNoTracking()
                .Where(r => r.ApplicationId == applicationId)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is not null &&
                (latest.Status == CvExtractionStatuses.Queued ||
                 latest.Status == CvExtractionStatuses.Processing))
                return Fail<CvExtractionResponse>(
                    "extraction_in_progress",
                    "An extraction run is already in progress.");

            var run = new CvExtractionRun
            {
                Id = Guid.NewGuid(),
                ApplicationId = application.Id,
                DocumentId = document.Id,
                Version = checked((latest?.Version ?? 0) + 1),
                SourceSha256Hash = document.Sha256Hash,
                ParserVersion =
                    CvStructuredFieldExtractorV2.ParserVersion,
                Status = CvExtractionStatuses.Queued,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.CvExtractionRuns.Add(run);

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvExtractionQueued",
                EntityType = "CvExtractionRun",
                EntityId = run.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(
                "extraction_queued",
                "CV extraction has been queued.",
                Map(run));
        }
        catch (DbUpdateException)
        {
            return Fail<CvExtractionResponse>(
                "extraction_write_conflict",
                "Extraction could not be queued. Please retry.");
        }
        catch (DbException)
        {
            return Fail<CvExtractionResponse>(
                "extraction_database_error",
                "Extraction is temporarily unavailable.");
        }
    }

    // API-085
    public async Task<CvExtractionOperationResponse<
        IReadOnlyList<CvExtractionResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
            return Fail<IReadOnlyList<CvExtractionResponse>>(
                "forbidden",
                "HR Manager access is required.");

        if (!await _db.CandidateApplications
                .AnyAsync(
                    a => a.Id == applicationId,
                    cancellationToken))
            return Fail<IReadOnlyList<CvExtractionResponse>>(
                "application_not_found",
                "Application was not found.");

        var runs = await _db.CvExtractionRuns
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId)
            .OrderByDescending(r => r.Version)
            .ToListAsync(cancellationToken);

        IReadOnlyList<CvExtractionResponse> history =
            runs.Select(r => Map(r, false))
                .ToList()
                .AsReadOnly();

        return Ok(
            "extraction_history_retrieved",
            "Extraction history retrieved.",
            history);
    }

    // API-086
    public async Task<
        CvExtractionOperationResponse<CvExtractionResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
            return Fail<CvExtractionResponse>(
                "forbidden",
                "HR Manager access is required.");

        var run = await _db.CvExtractionRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.Id == runId,
                cancellationToken);

        if (run is null)
            return Fail<CvExtractionResponse>(
                "extraction_not_found",
                "Extraction run was not found.");

        try
        {
            return Ok(
                "extraction_retrieved",
                "Extraction result retrieved.",
                Map(run));
        }
        catch (JsonException)
        {
            return Fail<CvExtractionResponse>(
                "extraction_result_invalid",
                "Stored extraction result is invalid.");
        }
    }

    private static JsonElement? ReadJson(string? json)
    {
        if (json is null)
            return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CvExtractionResponse Map(
        CvExtractionRun run,
        bool includeResult = true)
    {
        return new CvExtractionResponse
        {
            Id = run.Id,
            ApplicationId = run.ApplicationId,
            DocumentId = run.DocumentId,
            Version = run.Version,
            SourceSha256Hash = run.SourceSha256Hash,
            ParserVersion = run.ParserVersion,
            Status = run.Status,
            Result = includeResult
                ? ReadJson(run.ResultJson)
                : null,
            Evidence = includeResult
                ? ReadJson(run.EvidenceJson)
                : null,
            FailureCode = run.FailureCode,
            CreatedAtUtc = run.CreatedAtUtc,
            StartedAtUtc = run.StartedAtUtc,
            CompletedAtUtc = run.CompletedAtUtc
        };
    }
}
