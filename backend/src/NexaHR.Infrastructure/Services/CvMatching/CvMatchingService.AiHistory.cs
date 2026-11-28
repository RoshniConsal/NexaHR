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
    internal async Task<AiInputPreparationResult?>
        ReconstructHistoricalAiInputAsync(
            CvAiAnalysisRun run,
            CancellationToken cancellationToken)
    {
        var match = await _db.CvMatchRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == run.CvMatchRunId,
                cancellationToken);

        if (match is null ||
            match.ApplicationId != run.ApplicationId ||
            match.DocumentId != run.DocumentId ||
            match.CvReviewVersionId != run.CvReviewVersionId ||
            match.VacancyRevisionId != run.VacancyRevisionId ||
            match.RuleVersion != RuleBasedMatchingEngine.RuleVersion ||
            !string.Equals(
                match.SourceSha256Hash,
                run.SourceSha256Hash,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var review = await _db.CvReviewVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == run.CvReviewVersionId &&
                     x.ApplicationId == run.ApplicationId &&
                     x.DocumentId == run.DocumentId &&
                     x.VacancyRevisionId == run.VacancyRevisionId,
                cancellationToken);

        var catalogueVersion =
            await _db.SkillCatalogueVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == match.SkillCatalogueVersionId,
                    cancellationToken);

        if (review is null ||
            review.Status != "Confirmed" ||
            catalogueVersion is null ||
            !string.Equals(
                Hash(catalogueVersion.CatalogueJson),
                catalogueVersion.ContentSha256Hash,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var criteria = await _db.VacancyCriteria
            .AsNoTracking()
            .Where(x =>
                x.VacancyRevisionId == run.VacancyRevisionId)
            .ToListAsync(cancellationToken);

        try
        {
            var values = CvReviewJsonReader.ReadValues(
                review.ValuesJson);

            if (CvStructuredValuesValidator
                .ValidateConfirmation(values).Count != 0)
            {
                return null;
            }

            var catalogue =
                JsonSerializer.Deserialize<
                    List<SkillCatalogueEntry>>(
                    catalogueVersion.CatalogueJson,
                    JsonOptions);

            if (catalogue is null ||
                catalogue.Any(x =>
                    x is null ||
                    x.Synonyms is null ||
                    string.IsNullOrWhiteSpace(
                        x.CanonicalName)))
            {
                return null;
            }

            var index =
                RuleBasedMatchingEngine.BuildIndex(
                    catalogue);

            static string Normalize(string value) =>
                value.Normalize(NormalizationForm.FormKC)
                    .Trim()
                    .ToUpperInvariant();

            var canonicalLabels = catalogue.ToDictionary(
                x => Normalize(x.CanonicalName),
                x => x.CanonicalName,
                StringComparer.Ordinal);

            var recalculated =
                new RuleBasedMatchingEngine().Calculate(
                    criteria,
                    values.Skills,
                    catalogue);

            if (recalculated.Score != match.Score ||
                recalculated.MatchedWeight != match.MatchedWeight ||
                recalculated.TotalWeight != match.TotalWeight ||
                JsonSerializer.Serialize(
                    recalculated.Evidence,
                    JsonOptions) != match.EvidenceJson)
            {
                return null;
            }

            var expectedCriteria =
                JsonSerializer.Serialize(
                    criteria
                        .OrderBy(x => x.Kind,
                            StringComparer.Ordinal)
                        .ThenBy(x => x.Label,
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

            if (expectedCriteria !=
                match.CriteriaSnapshotJson)
            {
                return null;
            }

            var sources = new List<AiCriterionSource>();

            var seen = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var evidence in recalculated.Evidence)
            {
                if (!index.TryGetValue(
                        Normalize(evidence.Label),
                        out var canonical) ||
                    !canonicalLabels.TryGetValue(
                        canonical,
                        out var label) ||
                    !seen.Add(canonical) ||
                    evidence.Status is not
                        ("Matched" or "NotEvidenced"))
                {
                    return null;
                }

                sources.Add(new AiCriterionSource(
                    evidence.CriterionId,
                    evidence.Kind,
                    label,
                    evidence.Status == "Matched"));
            }

            var prepared =
                AiInputPreparationEngine.Prepare(
                    sources
                        .OrderBy(x => x.Kind,
                            StringComparer.Ordinal)
                        .ThenBy(x => x.Label,
                            StringComparer.Ordinal)
                        .ThenBy(x => x.SourceCriterionId)
                        .ToArray());

            if (prepared.InputJson != run.InputJson ||
                !string.Equals(
                    prepared.InputSha256Hash,
                    run.InputSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return prepared;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
