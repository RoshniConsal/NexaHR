using System.Data;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed partial class CvAiAnalysisService
{
    public async Task<CvAiOperationResponse<AiInputResponse>>
        GetPreparedInputAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId,
                cancellationToken))
        {
            return Failure<AiInputResponse>(
                "forbidden");
        }

        if (applicationId == Guid.Empty)
        {
            return Failure<AiInputResponse>(
                "invalid_application");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var loaded = await _matching.LoadAiSourceAsync(
            applicationId,
            cancellationToken);

        if (loaded.Source is null)
        {
            return Failure<AiInputResponse>(
                loaded.Code);
        }

        var source = loaded.Source;

        var configuration =
            await LoadConfigurationAsync(
                cancellationToken);

        var authorization =
            await _authorization.ValidateAsync(
                applicationId,
                cancellationToken);

        var authorizationCode =
            configuration.Code != "success"
                ? configuration.Code
                : authorization.Code;

        if (authorizationCode == "success" &&
            (!authorization.Allowed ||
             authorization.Policy is null ||
             authorization.Consent is null ||
             authorization.Policy.Id !=
                configuration.ConsentNotice?.ProcessingPolicyId))
        {
            authorizationCode =
                "invalid_ai_authorization";
        }

        await transaction.CommitAsync(
            cancellationToken);

        // Provider readiness check occurs outside
        // the database transaction.
        if (authorizationCode == "success")
        {
            authorizationCode =
                await _provider.CheckReadinessAsync(
                    configuration.Model!,
                    configuration.ModelDigest!,
                    cancellationToken);
        }

        return Success(
            new AiInputResponse(
                source.Application.Id,
                source.Document.Id,
                source.Revision.Id,
                source.Review.Id,
                source.Match.Id,
                authorization.Policy?.Id ??
                    configuration.ConsentNotice?.ProcessingPolicyId,
                authorization.Consent?.Id,
                configuration.Provider,
                configuration.Model,
                configuration.ModelDigest,
                source.Document.Sha256Hash,
                source.PreparedInput.InputSha256Hash,
                AiInputPreparationEngine.PromptVersion,
                AiInputPreparationEngine.SchemaVersion,
                AiOutputValidator.SchemaVersion,
                source.PreparedInput.Input,
                source.PreparedInput.CriterionReferences,
                source.PreparedInput.EvidenceReferences,
                authorizationCode == "success",
                authorizationCode,
                true));
    }
}
