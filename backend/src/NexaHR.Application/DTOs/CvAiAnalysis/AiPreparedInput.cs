namespace NexaHR.Application.DTOs.CvAiAnalysis;

public sealed record AiCriterionInput(
    string CriterionId,
    string Kind,
    string Label);

public sealed record AiEvidenceInput(
    string EvidenceId,
    string CriterionId);

public sealed record AiPreparedInput(
    int SchemaVersion,
    IReadOnlyList<AiCriterionInput> Criteria,
    IReadOnlyList<AiEvidenceInput> Evidence);

public sealed record AiCriterionSource(
    Guid SourceCriterionId,
    string Kind,
    string Label,
    bool IsEvidenced);

public sealed record AiEvidenceReference(
    string EvidenceId,
    Guid SourceCriterionId);

public sealed record AiCriterionReference(
    string CriterionId,
    Guid SourceCriterionId);

public sealed record AiInputPreparationResult(
    AiPreparedInput Input,
    string InputJson,
    string InputSha256Hash,
    IReadOnlyList<AiCriterionReference> CriterionReferences,
    IReadOnlyList<AiEvidenceReference> EvidenceReferences);
