using System.Data;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed partial class CvAiAnalysisService
{
    public async Task<
        CvAiOperationResponse<CvAiAnalysisResponse>>
        StartAsync(
            Guid actorUserId,
            Guid applicationId,
            StartCvAiAnalysisRequest request,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId,
                cancellationToken))
        {
            return Failure<CvAiAnalysisResponse>(
                "forbidden");
        }

        if (applicationId == Guid.Empty ||
            !IsValidStartRequest(request))
        {
            return Failure<CvAiAnalysisResponse>(
                "invalid_ai_request");
        }

        // Check provider readiness before acquiring
        // database transaction locks.
        var initialConfiguration =
            await LoadConfigurationAsync(
                cancellationToken);

        if (initialConfiguration.Code != "success")
        {
            return Failure<CvAiAnalysisResponse>(
                initialConfiguration.Code);
        }

        if (!MatchesConfiguration(
                request,
                initialConfiguration))
        {
            return Failure<CvAiAnalysisResponse>(
                "ai_configuration_changed");
        }

        var readinessCode =
            await _provider.CheckReadinessAsync(
                initialConfiguration.Model!,
                initialConfiguration.ModelDigest!,
                cancellationToken);

        if (readinessCode != "success")
        {
            return Failure<CvAiAnalysisResponse>(
                readinessCode);
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Revalidate the actor inside the
            // consistent queue creation transaction.
            if (!await IsAuthorizedHrAsync(
                    actorUserId,
                    cancellationToken))
            {
                return Failure<CvAiAnalysisResponse>(
                    "forbidden");
            }

            var configuration =
                await LoadConfigurationAsync(
                    cancellationToken);

            if (configuration.Code != "success")
            {
                return Failure<CvAiAnalysisResponse>(
                    configuration.Code);
            }

            if (!MatchesConfiguration(
                    request,
                    configuration))
            {
                return Failure<CvAiAnalysisResponse>(
                    "ai_configuration_changed");
            }

            var authorization =
                await _authorization.ValidateAsync(
                    applicationId,
                    cancellationToken);

            if (!authorization.Allowed)
            {
                return Failure<CvAiAnalysisResponse>(
                    authorization.Code);
            }

            if (authorization.Policy is null ||
                authorization.Consent is null)
            {
                return Failure<CvAiAnalysisResponse>(
                    "invalid_ai_authorization");
            }

            if (authorization.Policy.Id !=
                    request.ProcessingPolicyId ||
                authorization.Policy.Id !=
                    configuration.ConsentNotice?
                        .ProcessingPolicyId ||
                authorization.Consent.Id !=
                    request.ApplicationConsentId)
            {
                return Failure<CvAiAnalysisResponse>(
                    "ai_authorization_changed");
            }

            var loaded =
                await _matching.LoadAiSourceAsync(
                    applicationId,
                    cancellationToken);

            if (loaded.Source is null)
            {
                return Failure<CvAiAnalysisResponse>(
                    loaded.Code);
            }

            var source = loaded.Source;

            if (source.Document.Id !=
                    request.DocumentId ||
                source.Revision.Id !=
                    request.VacancyRevisionId ||
                source.Review.Id !=
                    request.CvReviewVersionId ||
                source.Match.Id !=
                    request.CvMatchRunId)
            {
                return Failure<CvAiAnalysisResponse>(
                    "ai_source_version_changed");
            }

            if (!string.Equals(
                    source.PreparedInput.InputSha256Hash,
                    request.InputSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure<CvAiAnalysisResponse>(
                    "ai_input_hash_mismatch");
            }

            var activeRunExists =
                await _db.CvAiAnalysisRuns
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.ApplicationId ==
                                applicationId &&
                            (
                                x.Status ==
                                    CvAiAnalysisStatuses.Queued ||
                                x.Status ==
                                    CvAiAnalysisStatuses.Processing
                            ),
                        cancellationToken);

            if (activeRunExists)
            {
                return Failure<CvAiAnalysisResponse>(
                    "ai_analysis_in_progress");
            }

            var latestVersion =
                await _db.CvAiAnalysisRuns
                    .Where(x =>
                        x.ApplicationId == applicationId)
                    .MaxAsync(
                        x => (int?)x.Version,
                        cancellationToken)
                ?? 0;

            var run = new CvAiAnalysisRun
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                DocumentId = source.Document.Id,
                VacancyRevisionId = source.Revision.Id,
                CvReviewVersionId = source.Review.Id,
                CvMatchRunId = source.Match.Id,

                ProcessingPolicyId =
                    authorization.Policy.Id,

                ApplicationConsentId =
                    authorization.Consent.Id,

                Version = checked(latestVersion + 1),

                Provider = configuration.Provider!,
                Model = configuration.Model!,
                ModelDigest = configuration.ModelDigest!,

                PromptVersion =
                    AiInputPreparationEngine.PromptVersion,

                InputSchemaVersion =
                    AiInputPreparationEngine.SchemaVersion,

                OutputSchemaVersion =
                    AiOutputValidator.SchemaVersion,

                SourceSha256Hash =
                    source.Document.Sha256Hash,

                InputSha256Hash =
                    source.PreparedInput.InputSha256Hash,

                // Only server-reconstructed,
                // minimized input is persisted.
                InputJson =
                    source.PreparedInput.InputJson,

                Status = CvAiAnalysisStatuses.Queued,
                AttemptCount = 0,
                ResultJson = null,
                FailureCode = null,

                RequestedByUserId = actorUserId,
                CreatedAtUtc = DateTime.UtcNow,
                StartedAtUtc = null,
                CompletedAtUtc = null
            };

            _db.CvAiAnalysisRuns.Add(run);

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvAiAnalysisQueued",
                EntityType = "CvAiAnalysisRun",
                EntityId = run.Id,
                Outcome = "success",
                CorrelationId = run.Id
            });

            await _db.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Success(
                new CvAiAnalysisResponse(
                    run.Id,
                    run.ApplicationId,
                    run.DocumentId,
                    run.VacancyRevisionId,
                    run.CvReviewVersionId,
                    run.CvMatchRunId,
                    run.ProcessingPolicyId,
                    run.ApplicationConsentId,
                    run.Version,
                    run.Provider,
                    run.Model,
                    run.ModelDigest,
                    run.PromptVersion,
                    run.InputSchemaVersion,
                    run.OutputSchemaVersion,
                    run.SourceSha256Hash,
                    run.InputSha256Hash,
                    run.Status,
                    false,
                    Array.Empty<string>(),
                    run.AttemptCount,
                    run.FailureCode,
                    run.RequestedByUserId,
                    run.CreatedAtUtc,
                    run.StartedAtUtc,
                    run.CompletedAtUtc,
                    true,
                    null,
                    null,
                    null,
                    null));
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return Failure<CvAiAnalysisResponse>(
                "ai_analysis_write_conflict");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return Failure<CvAiAnalysisResponse>(
                "ai_analysis_database_error");
        }
        catch (System.Data.Common.DbException)
        {
            _db.ChangeTracker.Clear();

            return Failure<CvAiAnalysisResponse>(
                "ai_analysis_database_error");
        }
    }

    private static bool IsValidStartRequest(
        StartCvAiAnalysisRequest? request)
    {
        if (request is null)
            return false;

        static bool IsHash(string? value)
        {
            return value is not null &&
                value.Length == 64 &&
                value.All(Uri.IsHexDigit);
        }

        return request.DocumentId != Guid.Empty &&
            request.VacancyRevisionId != Guid.Empty &&
            request.CvReviewVersionId != Guid.Empty &&
            request.CvMatchRunId != Guid.Empty &&
            request.ProcessingPolicyId != Guid.Empty &&
            request.ApplicationConsentId != Guid.Empty &&

            IsHash(request.InputSha256Hash) &&
            IsHash(request.ModelDigest) &&

            string.Equals(
                request.Provider,
                "Ollama",
                StringComparison.Ordinal) &&

            !string.IsNullOrWhiteSpace(request.Model) &&
            request.Model.Length <= 200 &&

            request.PromptVersion ==
                AiInputPreparationEngine.PromptVersion &&

            request.InputSchemaVersion ==
                AiInputPreparationEngine.SchemaVersion &&

            request.OutputSchemaVersion ==
                AiOutputValidator.SchemaVersion;
    }

    private static bool MatchesConfiguration(
        StartCvAiAnalysisRequest request,
        ConfigurationSnapshot configuration)
    {
        return configuration.Enabled &&
            configuration.Code == "success" &&

            string.Equals(
                request.Provider,
                configuration.Provider,
                StringComparison.Ordinal) &&

            string.Equals(
                request.Model,
                configuration.Model,
                StringComparison.Ordinal) &&

            string.Equals(
                request.ModelDigest,
                configuration.ModelDigest,
                StringComparison.OrdinalIgnoreCase) &&

            request.ProcessingPolicyId ==
                configuration.ConsentNotice?
                    .ProcessingPolicyId;
    }
}
