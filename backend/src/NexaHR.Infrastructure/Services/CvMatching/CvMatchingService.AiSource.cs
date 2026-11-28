using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Application.Services.CvMatching;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvMatching;

public sealed partial class CvMatchingService
{
    internal sealed record AiValidatedSource(
        CandidateApplication Application,
        CvDocument Document,
        CvReviewVersion Review,
        VacancyRevision Revision,
        CvMatchRun Match,
        SkillCatalogueVersion Catalogue,
        AiInputPreparationResult PreparedInput);

    internal async Task<
        (AiValidatedSource? Source, string Code)>
        LoadAiSourceAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
            return (null, "invalid_application");

        // Caller must enforce authorization and
        // maintain a consistent database transaction.

        var loaded = await LoadSourceAsync(
            applicationId,
            cancellationToken);

        if (loaded.Source is null)
            return (null, loaded.Code);

        var source = loaded.Source;

        if (!AiConsentGuard.IsEligibleApplicationStage(
                source.Application.Stage))
        {
            return (
                null,
                "application_not_eligible_for_ai");
        }

        if (!string.Equals(
                Hash(source.Catalogue.CatalogueJson),
                source.Catalogue.ContentSha256Hash,
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, "catalogue_integrity_error");
        }

        var match = await _db.CvMatchRuns
            .AsNoTracking()
            .Where(x =>
                x.ApplicationId == applicationId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null)
            return (null, "matching_run_required");

        var staleReasons = GetStaleReasons(
            match, source, "success");

        if (staleReasons.Count != 0)
            return (null, "matching_run_stale");

        try
        {
            var values = CvReviewJsonReader.ReadValues(
                source.Review.ValuesJson);

            if (CvStructuredValuesValidator
                    .ValidateConfirmation(values).Count != 0)
            {
                return (null, "invalid_matching_source");
            }

            var catalogue = JsonSerializer.Deserialize<
                List<SkillCatalogueEntry>>(
                    source.Catalogue.CatalogueJson,
                    JsonOptions);

            if (catalogue is null)
                return (null, "invalid_catalogue");

            if (catalogue.Any(x =>
                    x is null ||
                    x.Synonyms is null ||
                    string.IsNullOrWhiteSpace(
                        x.CanonicalName)))
            {
                return (null, "invalid_catalogue");
            }

            var index =
                RuleBasedMatchingEngine.BuildIndex(
                    catalogue);

            static string Normalize(string value)
            {
                return value
                    .Normalize(NormalizationForm.FormKC)
                    .Trim()
                    .ToUpperInvariant();
            }

            var canonicalLabels = catalogue.ToDictionary(
                x => Normalize(x.CanonicalName),
                x => x.CanonicalName,
                StringComparer.Ordinal);

            var recalculated =
                new RuleBasedMatchingEngine().Calculate(
                    source.Criteria,
                    values.Skills,
                    catalogue);

            if (recalculated.Score != match.Score ||
                recalculated.MatchedWeight !=
                    match.MatchedWeight ||
                recalculated.TotalWeight !=
                    match.TotalWeight)
            {
                return (
                    null,
                    "matching_evidence_integrity_error");
            }

            var expectedEvidence =
                JsonSerializer.Serialize(
                    recalculated.Evidence,
                    JsonOptions);

            if (!string.Equals(
                    expectedEvidence,
                    match.EvidenceJson,
                    StringComparison.Ordinal))
            {
                return (
                    null,
                    "matching_evidence_integrity_error");
            }

            var expectedCriteria =
                JsonSerializer.Serialize(
                    source.Criteria
                        .OrderBy(
                            x => x.Kind,
                            StringComparer.Ordinal)
                        .ThenBy(
                            x => x.Label,
                            StringComparer.Ordinal)
                        .ThenBy(x => x.Id)
                        .Select(x => new
                        {
                            x.Id,
                            x.Kind,
                            x.Label,
                            x.Weight
                        }),
                    JsonOptions);

            if (!string.Equals(
                    expectedCriteria,
                    match.CriteriaSnapshotJson,
                    StringComparison.Ordinal))
            {
                return (
                    null,
                    "matching_criteria_integrity_error");
            }

            var aiSources =
                new List<AiCriterionSource>();

            var seenCanonicalSkills =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (var evidence in
                recalculated.Evidence)
            {
                var normalized =
                    Normalize(evidence.Label);

                if (!index.TryGetValue(
                        normalized,
                        out var canonical) ||
                    !canonicalLabels.TryGetValue(
                        canonical,
                        out var approvedLabel))
                {
                    return (
                        null,
                        "ai_skill_not_in_catalogue");
                }

                if (!seenCanonicalSkills.Add(
                        canonical))
                {
                    return (
                        null,
                        "duplicate_skill_criteria");
                }

                if (evidence.Status is not
                    ("Matched" or "NotEvidenced"))
                {
                    return (
                        null,
                        "invalid_matching_evidence");
                }

                aiSources.Add(
                    new AiCriterionSource(
                        evidence.CriterionId,
                        evidence.Kind,
                        approvedLabel,
                        evidence.Status == "Matched"));
            }

            if (aiSources.Count == 0)
                return (null, "ai_no_skill_criteria");

            var orderedSources = aiSources
                .OrderBy(
                    x => x.Kind,
                    StringComparer.Ordinal)
                .ThenBy(
                    x => x.Label,
                    StringComparer.Ordinal)
                .ThenBy(x => x.SourceCriterionId)
                .ToArray();

            var prepared =
                AiInputPreparationEngine.Prepare(
                    orderedSources);

            return (
                new AiValidatedSource(
                    source.Application,
                    source.Document,
                    source.Review,
                    source.Revision,
                    match,
                    source.Catalogue,
                    prepared),
                "success");
        }
        catch (JsonException)
        {
            return (null, "invalid_ai_source");
        }
        catch (ArgumentException)
        {
            return (null, "invalid_ai_source");
        }
    }
}
