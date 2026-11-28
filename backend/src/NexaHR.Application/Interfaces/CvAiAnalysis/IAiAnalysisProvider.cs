using NexaHR.Application.DTOs.CvAiAnalysis;

namespace NexaHR.Application.Interfaces.CvAiAnalysis;

public sealed record AiProviderResult(
    bool Success,
    string Code,
    int AttemptCount,
    AiValidatedOutput? Output);

public interface IAiAnalysisProvider
{
    Task<string> CheckReadinessAsync(
        string model,
        string expectedDigest,
        CancellationToken cancellationToken);

    Task<AiProviderResult> AnalyzeAsync(
        AiPreparedInput input,
        string model,
        string expectedDigest,
        Func<int, CancellationToken, Task<string>>
            authorizeAttempt,
        CancellationToken cancellationToken);
}
