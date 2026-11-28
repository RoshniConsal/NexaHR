using System.Data;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Interfaces.CvAiAnalysis;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;
using NexaHR.Infrastructure.Services.CvMatching;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed class CvAiAnalysisProcessor
{
    private readonly NexaHRDbContext _db;
    private readonly CvMatchingService _matching;
    private readonly AiProcessingAuthorizationService _authorization;
    private readonly IAiAnalysisProvider _provider;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _configuration;
    private readonly IAuditWriter _audit;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public CvAiAnalysisProcessor(
        NexaHRDbContext db,
        CvMatchingService matching,
        AiProcessingAuthorizationService authorization,
        IAiAnalysisProvider provider,
        UserManager<ApplicationUser> users,
        IConfiguration configuration,
        IAuditWriter audit)
    {
        _db = db;
        _matching = matching;
        _authorization = authorization;
        _provider = provider;
        _users = users;
        _configuration = configuration;
        _audit = audit;
    }

    public async Task<bool> ProcessNextAsync(
        CancellationToken cancellationToken)
    {
        var run = await _db.CvAiAnalysisRuns
            .Where(x =>
                x.Status == CvAiAnalysisStatuses.Queued)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return false;

        run.Status = CvAiAnalysisStatuses.Processing;
        run.StartedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }

        AiValidatedOutput? output = null;
        string? failureCode = null;

        try
        {
            AiPreparedInput? input;

            await using (
                var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken))
            {
                var validation = await ValidateRunAsync(
                    run, cancellationToken);

                failureCode = validation.Code == "success"
                    ? null
                    : validation.Code;

                input = validation.Input;

                await transaction.CommitAsync(
                    cancellationToken);
            }

            if (failureCode is null && input is not null)
            {
                var result = await _provider.AnalyzeAsync(
                    input,
                    run.Model,
                    run.ModelDigest,
                    async (attempt, ct) =>
                    {
                        await using var transaction =
                            await _db.Database
                                .BeginTransactionAsync(
                                    IsolationLevel.Serializable,
                                    ct);

                        var validation =
                            await ValidateRunAsync(run, ct);

                        if (validation.Code != "success")
                            return validation.Code;

                        if (attempt < 1 ||
                            attempt > 2 ||
                            attempt != run.AttemptCount + 1)
                        {
                            return "invalid_ai_attempt";
                        }

                        // Persist authorization of the attempt
                        // before dispatching to the provider.
                        run.AttemptCount = attempt;

                        await _db.SaveChangesAsync(ct);

                        await transaction.CommitAsync(ct);

                        return "success";
                    },
                    cancellationToken);

                if (result.AttemptCount != run.AttemptCount)
                {
                    failureCode =
                        "ai_attempt_count_mismatch";
                }
                else if (!result.Success)
                {
                    failureCode = SafeCode(result.Code);
                }
                else if (result.Output is null ||
                         result.Output.SchemaVersion !=
                             run.OutputSchemaVersion ||
                         !result.Output.RequiresHumanReview ||
                         run.AttemptCount < 1)
                {
                    failureCode = "invalid_ai_output";
                }
                else
                {
                    output = result.Output;
                }
            }
            else
            {
                failureCode ??= "invalid_ai_input";
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return true;
        }
        catch (OperationCanceledException)
        {
            failureCode =
                cancellationToken.IsCancellationRequested
                    ? "ai_processing_interrupted"
                    : "ai_processing_timeout";
        }
        catch (Exception)
        {
            failureCode = "ai_processing_failed";
        }

        // Reload committed state. A failed transaction may
        // have left tracked values that were never committed.
        var runId = run.Id;

        _db.ChangeTracker.Clear();

        using var finalTimeout =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(30));

        var finalToken = finalTimeout.Token;

        await using var finalTransaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                finalToken);

        run = await _db.CvAiAnalysisRuns
            .SingleAsync(x => x.Id == runId, finalToken);

        if (run.Status != CvAiAnalysisStatuses.Processing)
        {
            await finalTransaction.RollbackAsync(finalToken);
            return true;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            failureCode = "ai_processing_interrupted";
        }

        // Revalidate before persisting successful output.
        // Revoked consent or changed sources discard output.
        if (failureCode is null && output is not null)
        {
            var finalValidation = await ValidateRunAsync(
                run, finalToken);

            if (finalValidation.Code != "success")
                failureCode = finalValidation.Code;
        }

        string? resultJson = null;

        if (failureCode is null && output is not null)
        {
            resultJson = JsonSerializer.Serialize(
                output, JsonOptions);

            if (System.Text.Encoding.Unicode
                    .GetByteCount(resultJson) > 524288)
            {
                failureCode = "ai_result_too_large";
                resultJson = null;
            }
        }

        run.CompletedAtUtc = DateTime.UtcNow;

        if (failureCode is null &&
            resultJson is not null &&
            run.AttemptCount is >= 1 and <= 2)
        {
            run.Status = CvAiAnalysisStatuses.Ready;
            run.ResultJson = resultJson;
            run.FailureCode = null;
        }
        else
        {
            run.Status = CvAiAnalysisStatuses.Failed;
            run.ResultJson = null;
            run.FailureCode = SafeCode(
                failureCode ?? "ai_processing_failed");
        }

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorRole = "System",
            Module = "Recruitment",
            EntityType = "CvAiAnalysisRun",
            EntityId = run.Id,
            CorrelationId = run.Id,
            Action = run.Status == CvAiAnalysisStatuses.Ready
                ? "CvAiAnalysisCompleted"
                : "CvAiAnalysisFailed",
            Outcome = run.Status == CvAiAnalysisStatuses.Ready
                ? "success"
                : "failure",
            ReasonCode = run.FailureCode
        });

        try
        {
            await _db.SaveChangesAsync(finalToken);
            await finalTransaction.CommitAsync(finalToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return true;
        }

        return true;
    }

    private async Task<(AiPreparedInput? Input, string Code)>
        ValidateRunAsync(
            CvAiAnalysisRun run,
            CancellationToken cancellationToken)
    {
        var actor = await _users.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == run.RequestedByUserId,
                cancellationToken);

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
        {
            return (null, "ai_requester_not_authorized");
        }

        var roles = await _users.GetRolesAsync(actor);

        if (roles.Count != 1 ||
            !roles.Contains(AppRoles.HRManager))
        {
            return (null, "ai_requester_not_authorized");
        }

        var authorization =
            await _authorization.ValidatePinnedAsync(
                run, cancellationToken);

        if (authorization != "success")
            return (null, authorization);

        if (!string.Equals(
                run.Provider,
                "Ollama",
                StringComparison.Ordinal) ||
            !string.Equals(
                run.Model,
                _configuration["Recruitment:Ai:Model"],
                StringComparison.Ordinal) ||
            !string.Equals(
                run.ModelDigest,
                _configuration["Recruitment:Ai:ModelDigest"],
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, "ai_model_configuration_changed");
        }

        if (run.PromptVersion !=
                AiInputPreparationEngine.PromptVersion ||
            run.InputSchemaVersion !=
                AiInputPreparationEngine.SchemaVersion ||
            run.OutputSchemaVersion != 1)
        {
            return (null, "ai_schema_version_changed");
        }

        var loaded = await _matching.LoadAiSourceAsync(
            run.ApplicationId,
            cancellationToken);

        if (loaded.Source is null)
            return (null, loaded.Code);

        var source = loaded.Source;

        if (source.Application.Id != run.ApplicationId ||
            source.Document.Id != run.DocumentId ||
            source.Revision.Id != run.VacancyRevisionId ||
            source.Review.Id != run.CvReviewVersionId ||
            source.Match.Id != run.CvMatchRunId)
        {
            return (null, "ai_source_version_changed");
        }

        if (!string.Equals(
                source.Document.Sha256Hash,
                run.SourceSha256Hash,
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, "ai_source_hash_mismatch");
        }

        if (!string.Equals(
                source.PreparedInput.InputSha256Hash,
                run.InputSha256Hash,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                source.PreparedInput.InputJson,
                run.InputJson,
                StringComparison.Ordinal))
        {
            return (null, "ai_input_integrity_error");
        }

        // Never dispatch raw persisted JSON.
        // Use freshly reconstructed, validated input.
        return (
            source.PreparedInput.Input,
            "success");
    }

    private static string SafeCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
            code.Length <= 100 &&
            Regex.IsMatch(
                code,
                @"\A[a-z][a-z0-9_]*\z")
            ? code
            : "ai_processing_failed";
    }
}
