using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed partial class CvAiAnalysisService
{
    public async Task<
        CvAiOperationResponse<AiConfigurationStatusResponse>>
        GetConfigurationStatusAsync(
            Guid actorUserId,
            CancellationToken cancellationToken)
    {
        var role = await GetActorRoleAsync(
            actorUserId,
            cancellationToken);

        if (role != AppRoles.HRManager &&
            role != AppRoles.Candidate)
        {
            return Failure<AiConfigurationStatusResponse>(
                "forbidden");
        }

        var configuration =
            await LoadConfigurationAsync(
                cancellationToken);

        if (role == AppRoles.Candidate)
        {
            // No model identity, digest, provider
            // readiness probe or internal failure reason.
            return Success(
                new AiConfigurationStatusResponse(
                    false,
                    false,
                    "consent_notice_only",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    configuration.ConsentNotice,
                    true,
                    true));
        }

        var readinessCode = configuration.Code;

        if (readinessCode == "success")
        {
            readinessCode =
                await _provider.CheckReadinessAsync(
                    configuration.Model!,
                    configuration.ModelDigest!,
                    cancellationToken);
        }

        return Success(
            new AiConfigurationStatusResponse(
                configuration.Enabled,
                readinessCode == "success",
                readinessCode,
                configuration.Provider,
                configuration.Model,
                configuration.ModelDigest,
                AiInputPreparationEngine.PromptVersion,
                AiInputPreparationEngine.SchemaVersion,
                AiOutputValidator.SchemaVersion,
                configuration.ConsentNotice,
                true,
                true));
    }
}
