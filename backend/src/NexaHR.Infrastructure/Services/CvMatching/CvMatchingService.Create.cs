using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.CvMatching;
using NexaHR.Application.Services.CvMatching;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvMatching;

public sealed partial class CvMatchingService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static string Hash(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
    }

    private static CvMatchingOperationResponse<T> Fail<T>(
        string code,
        string message)
        where T : class
    {
        return new(false, code, message, null);
    }

    public async Task<
        CvMatchingOperationResponse<CvMatchResponse>>
        CreateAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(actorUserId))
            return Fail<CvMatchResponse>(
                "forbidden",
                "Access denied.");

        if (applicationId == Guid.Empty)
            return Fail<CvMatchResponse>(
                "invalid_application",
                "Invalid application identifier.");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var loaded = await LoadSourceAsync(
                applicationId,
                cancellationToken);

            if (loaded.Source is null)
                return Fail<CvMatchResponse>(
                    loaded.Code,
                    "Matching prerequisites are not satisfied.");

            var source = loaded.Source;

            if (!string.Equals(
                Hash(source.Catalogue.CatalogueJson),
                source.Catalogue.ContentSha256Hash,
                StringComparison.OrdinalIgnoreCase))
            {
                return Fail<CvMatchResponse>(
                    "catalogue_integrity_error",
                    "Catalogue integrity verification failed.");
            }

            var catalogue = JsonSerializer.Deserialize<
                List<SkillCatalogueEntry>>(
                    source.Catalogue.CatalogueJson,
                    JsonOptions);

            if (catalogue is null)
                return Fail<CvMatchResponse>(
                    "invalid_catalogue",
                    "Skill catalogue is invalid.");

            var values = CvReviewJsonReader.ReadValues(
                source.Review.ValuesJson);

            CvStructuredValuesValidator.ValidateDraft(values);

            var catalogueIndex =
                RuleBasedMatchingEngine.BuildIndex(catalogue);

            var seenCriteria =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (var criterion in source.Criteria)
            {
                if (criterion.Kind !=
                        VacancyCriterionKinds.RequiredSkill &&
                    criterion.Kind !=
                        VacancyCriterionKinds.PreferredSkill)
                {
                    continue;
                }

                var normalized = criterion.Label
                    .Normalize(NormalizationForm.FormKC)
                    .Trim()
                    .ToUpperInvariant();

                var canonical =
                    catalogueIndex.TryGetValue(
                        normalized,
                        out var mapped)
                    ? mapped
                    : normalized;

                if (!seenCriteria.Add(canonical))
                {
                    return Fail<CvMatchResponse>(
                        "duplicate_skill_criteria",
                        "Vacancy contains overlapping skill criteria.");
                }
            }

            var engine = new RuleBasedMatchingEngine();

            var result = engine.Calculate(
                source.Criteria,
                values.Skills,
                catalogue);

            var experienceEngine =
                new ExperienceAssessmentEngine();

            var now = DateTime.UtcNow;

            var assessmentDate = DateOnly.FromDateTime(
                now.AddMinutes(330));

            var experience = experienceEngine.Assess(
                source.Revision.MinimumExperienceYears,
                values.Experience,
                assessmentDate);

            var flags = experienceEngine.GetReviewFlags(
                source.Criteria);

            var criteriaJson = JsonSerializer.Serialize(
                source.Criteria
                    .OrderBy(x => x.Kind, StringComparer.Ordinal)
                    .ThenBy(x => x.Label, StringComparer.Ordinal)
                    .ThenBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Kind,
                        x.Label,
                        x.Weight
                    }),
                JsonOptions);

            var evidenceJson = JsonSerializer.Serialize(
                result.Evidence,
                JsonOptions);

            var experienceJson = JsonSerializer.Serialize(
                new
                {
                    assessment = experience,
                    reviewFlags = flags
                },
                JsonOptions);

            var inputJson = JsonSerializer.Serialize(
                new
                {
                    applicationId,
                    documentId = source.Document.Id,
                    source.Document.Sha256Hash,
                    reviewId = source.Review.Id,
                    revisionId = source.Revision.Id,
                    catalogueId = source.Catalogue.Id,
                    source.Catalogue.ContentSha256Hash,
                    ruleVersion =
                        RuleBasedMatchingEngine.RuleVersion,
                    criteriaJson,
                    skills = values.Skills,
                    minimumExperienceYears =
                        source.Revision.MinimumExperienceYears,
                    experienceDates = values.Experience.Select(
                        x => new
                        {
                            x.StartYear,
                            x.StartMonth,
                            x.EndYear,
                            x.EndMonth,
                            x.IsCurrent
                        }),
                    assessmentDate
                },
                JsonOptions);

            var previousVersion = await _db.CvMatchRuns
                .Where(x => x.ApplicationId == applicationId)
                .MaxAsync(
                    x => (int?)x.Version,
                    cancellationToken) ?? 0;

            var run = new CvMatchRun
            {
                ApplicationId = applicationId,
                DocumentId = source.Document.Id,
                VacancyRevisionId = source.Revision.Id,
                CvReviewVersionId = source.Review.Id,
                SkillCatalogueVersionId = source.Catalogue.Id,
                Version = checked(previousVersion + 1),
                RuleVersion =
                    RuleBasedMatchingEngine.RuleVersion,
                SourceSha256Hash =
                    source.Document.Sha256Hash,
                InputSha256Hash = Hash(inputJson),
                MatchedWeight = result.MatchedWeight,
                TotalWeight = result.TotalWeight,
                Score = result.Score,
                CriteriaSnapshotJson = criteriaJson,
                EvidenceJson = evidenceJson,
                ExperienceAssessmentJson = experienceJson,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now
            };

            _db.CvMatchRuns.Add(run);

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvMatchCreated",
                EntityType = "CvMatchRun",
                EntityId = run.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new(
                true,
                "success",
                "Matching run created.",
                Map(run, [], true));
        }
        catch (JsonException)
        {
            _db.ChangeTracker.Clear();

            return Fail<CvMatchResponse>(
                "invalid_matching_source",
                "Matching source data is invalid.");
        }
        catch (ArgumentException)
        {
            _db.ChangeTracker.Clear();

            return Fail<CvMatchResponse>(
                "invalid_matching_source",
                "Matching source validation failed.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return Fail<CvMatchResponse>(
                "matching_write_conflict",
                "Matching could not be saved. Retry.");
        }
        catch (DbException)
        {
            _db.ChangeTracker.Clear();

            return Fail<CvMatchResponse>(
                "matching_database_error",
                "Matching database operation failed.");
        }
    }

    private static CvMatchResponse Map(
        CvMatchRun run,
        IReadOnlyList<string> staleReasons,
        bool includeDetails)
    {
        return new CvMatchResponse
        {
            Id = run.Id,
            ApplicationId = run.ApplicationId,
            DocumentId = run.DocumentId,
            VacancyRevisionId = run.VacancyRevisionId,
            CvReviewVersionId = run.CvReviewVersionId,
            SkillCatalogueVersionId =
                run.SkillCatalogueVersionId,
            Version = run.Version,
            RuleVersion = run.RuleVersion,
            SourceSha256Hash = run.SourceSha256Hash,
            InputSha256Hash = run.InputSha256Hash,
            MatchedWeight = run.MatchedWeight,
            TotalWeight = run.TotalWeight,
            Score = run.Score,
            ScoreStatus = run.Score.HasValue
                ? "Scored"
                : "NotScored",
            IsStale = staleReasons.Count > 0,
            StaleReasons = staleReasons,
            Criteria = includeDetails
                ? ReadJson(run.CriteriaSnapshotJson)
                : null,
            Evidence = includeDetails
                ? ReadJson(run.EvidenceJson)
                : null,
            ExperienceAssessment = includeDetails
                ? ReadJson(run.ExperienceAssessmentJson)
                : null,
            CreatedAtUtc = DateTime.SpecifyKind(
                run.CreatedAtUtc,
                DateTimeKind.Utc)
        };
    }

    private static JsonElement ReadJson(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}
