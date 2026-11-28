using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvMatching;
using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Application.Services.CvMatching;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvMatching;

public sealed partial class CvMatchingService
    : ICvMatchingService
{
    public async Task<
        CvMatchingOperationResponse<
            IReadOnlyList<CvMatchResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(actorUserId))
            return Fail<IReadOnlyList<CvMatchResponse>>(
                "forbidden",
                "Access denied.");

        if (applicationId == Guid.Empty)
            return Fail<IReadOnlyList<CvMatchResponse>>(
                "invalid_application",
                "Invalid application identifier.");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var exists = await _db
                .Set<CandidateApplication>()
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == applicationId,
                    cancellationToken);

            if (!exists)
                return Fail<IReadOnlyList<CvMatchResponse>>(
                    "application_not_found",
                    "Application not found.");

            var runs = await _db.CvMatchRuns
                .AsNoTracking()
                .Where(x =>
                    x.ApplicationId == applicationId)
                .OrderByDescending(x => x.Version)
                .ToListAsync(cancellationToken);

            var loaded = await LoadSourceAsync(
                applicationId,
                cancellationToken);

            var response = runs
                .Select(run => Map(
                    run,
                    GetStaleReasons(
                        run,
                        loaded.Source,
                        loaded.Code),
                    false))
                .ToList();

            await transaction.CommitAsync(
                cancellationToken);

            return new(
                true,
                "success",
                "Matching history retrieved.",
                response);
        }
        catch (DbException)
        {
            return Fail<IReadOnlyList<CvMatchResponse>>(
                "matching_database_error",
                "Matching history could not be retrieved.");
        }
    }

    public async Task<
        CvMatchingOperationResponse<CvMatchResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid matchId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(actorUserId))
            return Fail<CvMatchResponse>(
                "forbidden",
                "Access denied.");

        if (matchId == Guid.Empty)
            return Fail<CvMatchResponse>(
                "invalid_match",
                "Invalid matching identifier.");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var run = await _db.CvMatchRuns
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == matchId,
                    cancellationToken);

            if (run is null)
                return Fail<CvMatchResponse>(
                    "match_not_found",
                    "Matching result not found.");

            var loaded = await LoadSourceAsync(
                run.ApplicationId,
                cancellationToken);

            var response = Map(
                run,
                GetStaleReasons(
                    run,
                    loaded.Source,
                    loaded.Code),
                true);

            await transaction.CommitAsync(
                cancellationToken);

            return new(
                true,
                "success",
                "Matching result retrieved.",
                response);
        }
        catch (JsonException)
        {
            return Fail<CvMatchResponse>(
                "invalid_matching_result",
                "Saved matching result is invalid.");
        }
        catch (DbException)
        {
            return Fail<CvMatchResponse>(
                "matching_database_error",
                "Matching result could not be retrieved.");
        }
    }

    private static IReadOnlyList<string> GetStaleReasons(
        CvMatchRun run,
        MatchingSource? current,
        string sourceCode)
    {
        var reasons = new List<string>();

        if (run.RuleVersion !=
            RuleBasedMatchingEngine.RuleVersion)
        {
            reasons.Add("rule_version_changed");
        }

        if (current is null)
        {
            reasons.Add(sourceCode);

            return reasons;
        }

        if (run.DocumentId != current.Document.Id)
            reasons.Add("document_changed");

        if (!string.Equals(
            run.SourceSha256Hash,
            current.Document.Sha256Hash,
            StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("source_hash_changed");
        }

        if (run.CvReviewVersionId != current.Review.Id)
            reasons.Add("review_version_changed");

        if (run.VacancyRevisionId != current.Revision.Id)
            reasons.Add("vacancy_revision_changed");

        if (run.SkillCatalogueVersionId !=
            current.Catalogue.Id)
        {
            reasons.Add("skill_catalogue_version_changed");
        }

        if (!string.Equals(
            Hash(current.Catalogue.CatalogueJson),
            current.Catalogue.ContentSha256Hash,
            StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("catalogue_integrity_error");
        }

        return reasons;
    }
}
