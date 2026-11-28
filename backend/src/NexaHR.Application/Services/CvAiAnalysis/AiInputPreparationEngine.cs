using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexaHR.Application.DTOs.CvAiAnalysis;

namespace NexaHR.Application.Services.CvAiAnalysis;

public static class AiInputPreparationEngine
{
    public const int SchemaVersion = 1;

    public const string PromptVersion =
        "nexahr-evidence-analysis-v1";

    public const int MaximumCriteria = 100;

    public const int MaximumInputBytes = 65536;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static AiInputPreparationResult Prepare(
        IReadOnlyList<AiCriterionSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0 ||
            sources.Count > MaximumCriteria)
        {
            throw new ArgumentException(
                "Invalid AI criterion count.");
        }

        var criteria = new List<AiCriterionInput>();
        var evidence = new List<AiEvidenceInput>();

        var criterionReferences =
            new List<AiCriterionReference>();

        var evidenceReferences =
            new List<AiEvidenceReference>();

        var sourceIds = new HashSet<Guid>();

        var labels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (source is null ||
                source.SourceCriterionId == Guid.Empty ||
                !sourceIds.Add(source.SourceCriterionId))
            {
                throw new ArgumentException(
                    "Invalid or duplicate criterion source.");
            }

            if (source.Kind is not
                ("RequiredSkill" or "PreferredSkill"))
            {
                throw new ArgumentException(
                    "Unsupported AI criterion kind.");
            }

            var label = ValidateLabel(source.Label);

            if (!labels.Add(label))
            {
                throw new ArgumentException(
                    "Duplicate AI criterion label.");
            }

            var criterionId =
                $"C{criteria.Count + 1}";

            criteria.Add(
                new AiCriterionInput(
                    criterionId,
                    source.Kind,
                    label));

            criterionReferences.Add(
                new AiCriterionReference(
                    criterionId,
                    source.SourceCriterionId));

            if (!source.IsEvidenced)
            {
                continue;
            }

            var evidenceId =
                $"E{evidence.Count + 1}";

            evidence.Add(
                new AiEvidenceInput(
                    evidenceId,
                    criterionId));

            evidenceReferences.Add(
                new AiEvidenceReference(
                    evidenceId,
                    source.SourceCriterionId));
        }

        var input = new AiPreparedInput(
            SchemaVersion,
            criteria.AsReadOnly(),
            evidence.AsReadOnly());

        var json = JsonSerializer.Serialize(
            input, JsonOptions);

        var bytes = Encoding.UTF8.GetBytes(json);

        if (bytes.Length > MaximumInputBytes)
        {
            throw new ArgumentException(
                "AI input exceeds permitted size.");
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(bytes));

        return new AiInputPreparationResult(
            input,
            json,
            hash,
            criterionReferences.AsReadOnly(),
            evidenceReferences.AsReadOnly());
    }

    private static string ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label) ||
            label.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Invalid AI criterion label.");
        }

        var normalized = label
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        if (normalized.Length is < 1 or > 100 ||
            normalized.Contains('@') ||
            normalized.Contains("://",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unsafe AI criterion label.");
        }

        if (normalized.Any(character =>
            !char.IsLetterOrDigit(character) &&
            character is not
                (' ' or '#' or '+' or '.' or
                 '-' or '_' or '/' or '(' or ')')))
        {
            throw new ArgumentException(
                "Unsupported AI criterion characters.");
        }

        return normalized;
    }
}
