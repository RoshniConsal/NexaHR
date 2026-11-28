using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed partial class CvAiAnalysisService
{
    private static readonly JsonSerializerOptions
        HistoryJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };

    public async Task<
        CvAiOperationResponse<
            IReadOnlyList<CvAiAnalysisResponse>>>
        GetHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
        {
            return Failure<
                IReadOnlyList<CvAiAnalysisResponse>>(
                    "forbidden", "Access denied.");
        }

        if (applicationId == Guid.Empty)
            return Failure<
                IReadOnlyList<CvAiAnalysisResponse>>(
                    "invalid_application");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await _db.CandidateApplications
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == applicationId,
                    cancellationToken))
            {
                return Failure<
                    IReadOnlyList<CvAiAnalysisResponse>>(
                        "application_not_found");
            }

            var runs = await _db.CvAiAnalysisRuns
                .AsNoTracking()
                .Where(x =>
                    x.ApplicationId == applicationId)
                .OrderByDescending(x => x.Version)
                .ToListAsync(cancellationToken);

            var current =
                await _matching.LoadAiSourceAsync(
                    applicationId,
                    cancellationToken);

            var configuration =
                await LoadConfigurationAsync(
                    cancellationToken);

            var authorization =
                await _authorization.ValidateAsync(
                    applicationId,
                    cancellationToken);

            var response =
                new List<CvAiAnalysisResponse>();

            foreach (var run in runs)
            {
                var reasons = GetAiStaleReasons(
                    run,
                    current.Source,
                    current.Code,
                    configuration,
                    authorization);

                response.Add(MapHistoricalAnalysis(
                    run, reasons, null, null));
            }

            await transaction.CommitAsync(
                cancellationToken);

            return Success<
                IReadOnlyList<CvAiAnalysisResponse>>(
                    response);
        }
        catch (DbException)
        {
            return Failure<
                IReadOnlyList<CvAiAnalysisResponse>>(
                    "ai_database_error");
        }
    }

    public async Task<
        CvAiOperationResponse<CvAiAnalysisResponse>>
        GetResultAsync(
            Guid actorUserId,
            Guid analysisId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
        {
            return Failure<CvAiAnalysisResponse>(
                "forbidden", "Access denied.");
        }

        if (analysisId == Guid.Empty)
            return Failure<CvAiAnalysisResponse>(
                "invalid_analysis");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var run = await _db.CvAiAnalysisRuns
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == analysisId,
                    cancellationToken);

            if (run is null)
                return Failure<CvAiAnalysisResponse>(
                    "analysis_not_found");

            var current =
                await _matching.LoadAiSourceAsync(
                    run.ApplicationId,
                    cancellationToken);

            var configuration =
                await LoadConfigurationAsync(
                    cancellationToken);

            var authorization =
                await _authorization.ValidateAsync(
                    run.ApplicationId,
                    cancellationToken);

            var reasons = GetAiStaleReasons(
                run,
                current.Source,
                current.Code,
                configuration,
                authorization);

            // History remains available as metadata.
            // Suppress detailed AI content when current
            // processing authorization is unavailable.
            if (!authorization.Allowed)
            {
                var metadata = MapHistoricalAnalysis(
                    run, reasons, null, null);

                await transaction.CommitAsync(
                    cancellationToken);

                return Success(metadata);
            }

            var prepared =
                await _matching
                    .ReconstructHistoricalAiInputAsync(
                        run, cancellationToken);

            if (prepared is null)
                return Failure<CvAiAnalysisResponse>(
                    "ai_historical_source_invalid");

            AiValidatedOutput? result = null;

            if (run.Status == CvAiAnalysisStatuses.Ready)
            {
                result = ValidateSavedResult(
                    run, prepared.Input);

                if (result is null)
                    return Failure<CvAiAnalysisResponse>(
                        "ai_saved_result_invalid");
            }

            var response = MapHistoricalAnalysis(
                run, reasons, prepared, result);

            await transaction.CommitAsync(
                cancellationToken);

            return Success(response);
        }
        catch (JsonException)
        {
            return Failure<CvAiAnalysisResponse>(
                "ai_saved_result_invalid");
        }
        catch (ArgumentException)
        {
            return Failure<CvAiAnalysisResponse>(
                "ai_saved_result_invalid");
        }
        catch (DbException)
        {
            return Failure<CvAiAnalysisResponse>(
                "ai_database_error");
        }
    }

    private static IReadOnlyList<string> GetAiStaleReasons(
        CvAiAnalysisRun run,
        NexaHR.Infrastructure.Services.CvMatching
            .CvMatchingService.AiValidatedSource? current,
        string sourceCode,
        ConfigurationSnapshot configuration,
        AiProcessingAuthorizationService
            .AuthorizationResult authorization)
    {
        var reasons = new List<string>();

        if (current is null)
        {
            reasons.Add(sourceCode);
        }
        else
        {
            if (run.DocumentId != current.Document.Id)
                reasons.Add("document_changed");

            if (run.CvReviewVersionId != current.Review.Id)
                reasons.Add("review_version_changed");

            if (run.VacancyRevisionId != current.Revision.Id)
                reasons.Add("vacancy_revision_changed");

            if (run.CvMatchRunId != current.Match.Id)
                reasons.Add("matching_run_changed");

            if (!string.Equals(
                run.SourceSha256Hash,
                current.Document.Sha256Hash,
                StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("source_hash_changed");
            }

            if (!string.Equals(
                run.InputSha256Hash,
                current.PreparedInput.InputSha256Hash,
                StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("ai_input_changed");
            }
        }

        if (configuration.Code != "success")
        {
            reasons.Add(configuration.Code);
        }
        else
        {
            if (run.Provider != configuration.Provider)
                reasons.Add("ai_provider_changed");

            if (run.Model != configuration.Model ||
                !string.Equals(
                    run.ModelDigest,
                    configuration.ModelDigest,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("ai_model_changed");
            }
        }

        if (run.PromptVersion !=
                AiInputPreparationEngine.PromptVersion ||
            run.InputSchemaVersion !=
                AiInputPreparationEngine.SchemaVersion ||
            run.OutputSchemaVersion !=
                AiOutputValidator.SchemaVersion)
        {
            reasons.Add("ai_schema_or_prompt_changed");
        }

        if (!authorization.Allowed)
        {
            reasons.Add(authorization.Code);
        }
        else
        {
            if (authorization.Policy?.Id !=
                run.ProcessingPolicyId)
            {
                reasons.Add("ai_processing_policy_changed");
            }

            if (authorization.Consent?.Id !=
                run.ApplicationConsentId)
            {
                reasons.Add("ai_consent_changed");
            }
        }

        return reasons
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static AiValidatedOutput? ValidateSavedResult(
        CvAiAnalysisRun run,
        AiPreparedInput input)
    {
        if (string.IsNullOrWhiteSpace(run.ResultJson) ||
            run.ResultJson.Length > 262144 ||
            run.OutputSchemaVersion !=
                AiOutputValidator.SchemaVersion)
        {
            return null;
        }

        var saved =
            JsonSerializer.Deserialize<AiValidatedOutput>(
                run.ResultJson,
                HistoryJsonOptions);

        if (saved is null ||
            saved.SchemaVersion != run.OutputSchemaVersion ||
            !saved.RequiresHumanReview ||
            saved.Strengths is null ||
            saved.Gaps is null ||
            saved.InterviewSuggestions is null ||
            saved.Strengths.Any(x => x is null) ||
            saved.Gaps.Any(x => x is null) ||
            saved.InterviewSuggestions.Any(x => x is null))
        {
            return null;
        }

        var validationInput = JsonSerializer.Serialize(
            new
            {
                strengths = saved.Strengths.Select(x => new
                {
                    x.CriterionId,
                    x.EvidenceId
                }),
                gaps = saved.Gaps.Select(x =>
                    x.CriterionId),
                interviewSuggestions =
                    saved.InterviewSuggestions.Select(
                        x => new
                        {
                            x.CriterionId,
                            x.Question
                        })
            },
            HistoryJsonOptions);

        var validated = AiOutputValidator.Validate(
            input, validationInput);

        if (!validated.Success ||
            validated.Output is null)
        {
            return null;
        }

        var expectedJson = JsonSerializer.Serialize(
            validated.Output,
            HistoryJsonOptions);

        if (!string.Equals(
            expectedJson,
            run.ResultJson,
            StringComparison.Ordinal))
        {
            return null;
        }

        return validated.Output;
    }

    private static CvAiAnalysisResponse MapHistoricalAnalysis(
        CvAiAnalysisRun run,
        IReadOnlyList<string> reasons,
        AiInputPreparationResult? prepared,
        AiValidatedOutput? result)
    {
        return new CvAiAnalysisResponse(
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
            reasons.Count != 0,
            reasons,
            run.AttemptCount,
            run.FailureCode,
            run.RequestedByUserId,
            DateTime.SpecifyKind(
                run.CreatedAtUtc, DateTimeKind.Utc),
            run.StartedAtUtc.HasValue
                ? DateTime.SpecifyKind(
                    run.StartedAtUtc.Value,
                    DateTimeKind.Utc)
                : null,
            run.CompletedAtUtc.HasValue
                ? DateTime.SpecifyKind(
                    run.CompletedAtUtc.Value,
                    DateTimeKind.Utc)
                : null,
            true,
            prepared?.Input,
            result,
            prepared?.CriterionReferences,
            prepared?.EvidenceReferences);
    }
}
