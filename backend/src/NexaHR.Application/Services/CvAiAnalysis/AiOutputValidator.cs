using System.Text;
using System.Text.Json;
using NexaHR.Application.DTOs.CvAiAnalysis;

namespace NexaHR.Application.Services.CvAiAnalysis;

public static class AiOutputValidator
{
    public const int SchemaVersion = 1;

    public const int MaximumOutputBytes = 65536;

    public const int MaximumInterviewSuggestions = 10;

    private static AiOutputValidationResult Fail(
        string code)
    {
        return new(false, code, null);
    }

    public static AiOutputValidationResult Validate(
        AiPreparedInput input,
        string responseJson)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(responseJson))
            return Fail("ai_empty_response");

        if (Encoding.UTF8.GetByteCount(responseJson) >
            MaximumOutputBytes)
        {
            return Fail("ai_response_too_large");
        }

        try
        {
            var criteria = input.Criteria.ToDictionary(
                x => x.CriterionId,
                StringComparer.Ordinal);

            var evidence = input.Evidence.ToDictionary(
                x => x.EvidenceId,
                StringComparer.Ordinal);

            var evidenceByCriterion =
                input.Evidence.ToDictionary(
                    x => x.CriterionId,
                    StringComparer.Ordinal);

            if (input.SchemaVersion != 1 ||
                criteria.Count == 0 ||
                evidence.Values.Any(x =>
                    !criteria.ContainsKey(x.CriterionId)))
            {
                return Fail("ai_invalid_input");
            }

            using var document = JsonDocument.Parse(
                responseJson,
                new JsonDocumentOptions
                {
                    MaxDepth = 12,
                    AllowTrailingCommas = false,
                    CommentHandling =
                        JsonCommentHandling.Disallow
                });

            var root = document.RootElement;

            if (!HasExactProperties(
                    root,
                    "strengths",
                    "gaps",
                    "interviewSuggestions"))
            {
                return Fail("ai_invalid_schema");
            }

            var strengthsJson =
                root.GetProperty("strengths");

            var gapsJson =
                root.GetProperty("gaps");

            var suggestionsJson =
                root.GetProperty(
                    "interviewSuggestions");

            if (strengthsJson.ValueKind !=
                    JsonValueKind.Array ||
                gapsJson.ValueKind !=
                    JsonValueKind.Array ||
                suggestionsJson.ValueKind !=
                    JsonValueKind.Array)
            {
                return Fail("ai_invalid_schema");
            }

            if (strengthsJson.GetArrayLength() !=
                    evidence.Count ||
                gapsJson.GetArrayLength() !=
                    criteria.Count - evidence.Count)
            {
                return Fail(
                    "ai_incomplete_criterion_coverage");
            }

            if (suggestionsJson.GetArrayLength() >
                MaximumInterviewSuggestions)
            {
                return Fail(
                    "ai_too_many_interview_suggestions");
            }

            var strengths =
                new List<AiStrengthOutput>();

            var gaps =
                new List<AiGapOutput>();

            var suggestions =
                new List<AiInterviewSuggestion>();

            var coveredCriteria =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (var item in
                strengthsJson.EnumerateArray())
            {
                if (!HasExactProperties(
                        item,
                        "criterionId",
                        "evidenceId"))
                {
                    return Fail("ai_invalid_strength");
                }

                var criterionId =
                    ReadString(item, "criterionId");

                var evidenceId =
                    ReadString(item, "evidenceId");

                if (criterionId is null ||
                    evidenceId is null ||
                    !criteria.TryGetValue(
                        criterionId, out var criterion) ||
                    !evidence.TryGetValue(
                        evidenceId, out var source) ||
                    source.CriterionId != criterionId ||
                    !coveredCriteria.Add(criterionId))
                {
                    return Fail(
                        "ai_invalid_evidence_reference");
                }

                strengths.Add(
                    new AiStrengthOutput(
                        criterionId,
                        evidenceId,
                        $"Reviewed CV evidence supports {criterion.Label}."));
            }

            foreach (var item in
                gapsJson.EnumerateArray())
            {
                if (item.ValueKind !=
                    JsonValueKind.String)
                {
                    return Fail("ai_invalid_gap");
                }

                var criterionId = item.GetString();

                if (criterionId is null ||
                    !criteria.TryGetValue(
                        criterionId, out var criterion) ||
                    evidenceByCriterion.ContainsKey(
                        criterionId) ||
                    !coveredCriteria.Add(criterionId))
                {
                    return Fail(
                        "ai_invalid_gap_reference");
                }

                gaps.Add(
                    new AiGapOutput(
                        criterionId,
                        $"{criterion.Label} is not evidenced in the confirmed CV review. This does not establish inability."));
            }

            if (coveredCriteria.Count != criteria.Count)
            {
                return Fail(
                    "ai_incomplete_criterion_coverage");
            }

            var uniqueQuestions =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var item in
                suggestionsJson.EnumerateArray())
            {
                if (!HasExactProperties(
                        item,
                        "criterionId",
                        "question"))
                {
                    return Fail(
                        "ai_invalid_interview_suggestion");
                }

                var criterionId =
                    ReadString(item, "criterionId");

                var question =
                    ReadString(item, "question");

                if (criterionId is null ||
                    !criteria.ContainsKey(criterionId) ||
                    question is null)
                {
                    return Fail(
                        "ai_invalid_interview_suggestion");
                }

                question = question.Trim();

                if (question.Length is < 10 or > 300 ||
                    question.Any(char.IsControl) ||
                    question.Contains('@') ||
                    question.Contains(
                        "://", StringComparison.Ordinal) ||
                    !question.EndsWith(
                        "?", StringComparison.Ordinal) ||
                    !uniqueQuestions.Add(question))
                {
                    return Fail(
                        "ai_invalid_interview_question");
                }

                // Preserve the model-selected criterion,
                // but never expose untrusted generated questions.
                var label = criteria[criterionId].Label;

                var neutralQuestion =
                    $"How would you apply {label} to solve a relevant technical problem?";

                if (suggestions.Any(x =>
                        x.CriterionId == criterionId))
                {
                    continue;
                }

                suggestions.Add(
                    new AiInterviewSuggestion(
                        criterionId,
                        neutralQuestion));
            }

            var output = new AiValidatedOutput(
                SchemaVersion,
                strengths.AsReadOnly(),
                gaps.AsReadOnly(),
                suggestions.AsReadOnly(),
                true);

            return new(
                true,
                "success",
                output);
        }
        catch (JsonException)
        {
            return Fail("ai_invalid_json");
        }
        catch (ArgumentException)
        {
            return Fail("ai_invalid_input");
        }
    }

    private static string? ReadString(
        JsonElement item,
        string property)
    {
        if (!item.TryGetProperty(
                property, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool HasExactProperties(
        JsonElement item,
        params string[] expected)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var property in
            item.EnumerateObject())
        {
            if (!seen.Add(property.Name) ||
                !expected.Contains(
                    property.Name,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }

        return seen.Count == expected.Length;
    }
}
