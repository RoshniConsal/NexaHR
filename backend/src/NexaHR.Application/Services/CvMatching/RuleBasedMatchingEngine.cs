using System.Text;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Application.Services.CvMatching;

public sealed record SkillCatalogueEntry(
    string CanonicalName,
    IReadOnlyList<string> Synonyms);

public sealed record SkillMatchEvidence(
    Guid CriterionId,
    string Kind,
    string Label,
    decimal Weight,
    string Status,
    int? ReviewedSkillIndex,
    string? ReviewedSkill,
    decimal AwardedWeight);

public sealed record RuleBasedMatchResult(
    decimal MatchedWeight,
    decimal TotalWeight,
    decimal? Score,
    IReadOnlyList<SkillMatchEvidence> Evidence);

public sealed class RuleBasedMatchingEngine
{
    public const string RuleVersion =
        "nexahr-weighted-skills-v1";

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "Skill cannot be empty.");

        return value
            .Normalize(NormalizationForm.FormKC)
            .Trim()
            .ToUpperInvariant();
    }

    public static Dictionary<string, string> BuildIndex(
        IReadOnlyList<SkillCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        var index = new Dictionary<string, string>(
            StringComparer.Ordinal);

        var canonicalNames = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var entry in catalogue)
        {
            var canonical = Normalize(
                entry.CanonicalName);

            if (!canonicalNames.Add(canonical))
                throw new ArgumentException(
                    "Duplicate canonical skill.");

            Register(canonical, canonical);

            foreach (var synonym in entry.Synonyms)
                Register(Normalize(synonym), canonical);
        }

        return index;

        void Register(string alias, string canonical)
        {
            if (index.TryGetValue(alias, out var existing)
                && existing != canonical)
            {
                throw new ArgumentException(
                    "Ambiguous skill synonym.");
            }

            index[alias] = canonical;
        }
    }

    public RuleBasedMatchResult Calculate(
        IReadOnlyList<VacancyCriterion> criteria,
        IReadOnlyList<string> reviewedSkills,
        IReadOnlyList<SkillCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(reviewedSkills);

        var index = BuildIndex(catalogue);

        string Resolve(string value)
        {
            var normalized = Normalize(value);

            return index.TryGetValue(
                normalized, out var canonical)
                ? canonical
                : normalized;
        }

        var skills = reviewedSkills
            .Select((skill, position) => new
            {
                Skill = skill,
                Position = position,
                Canonical = Resolve(skill)
            })
            .ToList();

        var evidence = new List<SkillMatchEvidence>();

        decimal matchedWeight = 0;
        decimal totalWeight = 0;

        foreach (var criterion in criteria
            .OrderBy(c => c.Kind, StringComparer.Ordinal)
            .ThenBy(c => c.Label, StringComparer.Ordinal)
            .ThenBy(c => c.Id))
        {
            decimal expectedWeight;

            switch (criterion.Kind)
            {
                case VacancyCriterionKinds.RequiredSkill:
                    expectedWeight = 2;
                    break;

                case VacancyCriterionKinds.PreferredSkill:
                    expectedWeight = 1;
                    break;

                case VacancyCriterionKinds.InterviewCriterion:
                case VacancyCriterionKinds.ReviewFlag:
                    continue;

                default:
                    throw new ArgumentException(
                        "Unknown criterion kind.");
            }

            if (criterion.Weight != expectedWeight)
                throw new ArgumentException(
                    "Invalid criterion weight.");

            var canonical = Resolve(criterion.Label);

            var match = skills.FirstOrDefault(
                s => s.Canonical == canonical);

            var awarded = match is null
                ? 0m
                : expectedWeight;

            totalWeight += expectedWeight;
            matchedWeight += awarded;

            evidence.Add(new SkillMatchEvidence(
                criterion.Id,
                criterion.Kind,
                criterion.Label,
                expectedWeight,
                match is null
                    ? "NotEvidenced"
                    : "Matched",
                match?.Position,
                match?.Skill,
                awarded));
        }

        decimal? score = totalWeight == 0
            ? null
            : Math.Round(
                100m * matchedWeight / totalWeight,
                2,
                MidpointRounding.AwayFromZero);

        return new RuleBasedMatchResult(
            matchedWeight,
            totalWeight,
            score,
            evidence);
    }
}
