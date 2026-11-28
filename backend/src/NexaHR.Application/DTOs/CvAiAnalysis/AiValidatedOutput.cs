namespace NexaHR.Application.DTOs.CvAiAnalysis;

public sealed record AiStrengthOutput(
    string CriterionId,
    string EvidenceId,
    string Summary);

public sealed record AiGapOutput(
    string CriterionId,
    string Summary);

public sealed record AiInterviewSuggestion(
    string CriterionId,
    string Question);

public sealed record AiValidatedOutput(
    int SchemaVersion,
    IReadOnlyList<AiStrengthOutput> Strengths,
    IReadOnlyList<AiGapOutput> Gaps,
    IReadOnlyList<AiInterviewSuggestion> InterviewSuggestions,
    bool RequiresHumanReview);

public sealed record AiOutputValidationResult(
    bool Success,
    string Code,
    AiValidatedOutput? Output);
