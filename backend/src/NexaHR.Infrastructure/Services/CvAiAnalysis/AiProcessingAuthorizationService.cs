using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexaHR.Application.Services.CvAiAnalysis;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed class AiProcessingAuthorizationService
{
    private readonly NexaHRDbContext _db;
    private readonly IConfiguration _configuration;

    public AiProcessingAuthorizationService(
        NexaHRDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public sealed record AuthorizationResult(
        bool Allowed,
        string Code,
        ProcessingPolicy? Policy,
        ApplicationConsent? Consent);

    private static AuthorizationResult Denied(
        string code)
    {
        return new AuthorizationResult(
            false, code, null, null);
    }

    public async Task<AuthorizationResult>
        ValidateAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        // The caller must authorize the HR actor.
        // Queue creation must use a consistent transaction.
        // Workers must revalidate before provider dispatch.

        if (applicationId == Guid.Empty)
            return Denied("invalid_application");

        if (!bool.TryParse(
                _configuration[
                    "Recruitment:Ai:Enabled"],
                out var enabled) ||
            !enabled)
        {
            return Denied("ai_disabled");
        }

        var provider =
            _configuration["Recruitment:Ai:Provider"];

        if (string.IsNullOrWhiteSpace(provider) ||
            !string.Equals(
                provider,
                "Ollama",
                StringComparison.Ordinal))
        {
            return Denied(
                "ai_provider_not_configured");
        }

        if (!Guid.TryParse(
                _configuration[
                    "Recruitment:Ai:ApprovedPolicyId"],
                out var approvedPolicyId) ||
            approvedPolicyId == Guid.Empty)
        {
            return Denied(
                "ai_policy_not_enabled");
        }

        var application =
            await _db.CandidateApplications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == applicationId,
                    cancellationToken);

        if (application is null)
            return Denied("application_not_found");

        if (!AiConsentGuard
                .IsEligibleApplicationStage(
                    application.Stage))
        {
            return Denied(
                "application_not_eligible_for_ai");
        }

        var candidate = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id ==
                    application.CandidateUserId,
                cancellationToken);

        if (candidate is null ||
            !candidate.IsActive ||
            !candidate.EmailConfirmed)
        {
            return Denied(
                "candidate_account_not_eligible");
        }

        var policy = await _db.ProcessingPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var policyCode =
            AiConsentGuard.ValidatePolicy(
                policy,
                provider,
                approvedPolicyId,
                now);

        if (policyCode != "success" ||
            policy is null)
        {
            return Denied(policyCode);
        }

        // Do not filter by Granted or policy ID here.
        // A newer revocation must not be bypassed
        // by selecting an older granted consent.

        var consent = await _db.ApplicationConsents
            .AsNoTracking()
            .Where(x =>
                x.ApplicationId == applicationId &&
                x.Purpose ==
                    ApplicationConsentPurposes.AiProcessing &&
                x.Provider == provider)
            .OrderByDescending(x => x.AtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var consentCode =
            AiConsentGuard.ValidateConsent(
                application,
                policy,
                consent,
                DateTime.UtcNow);

        if (consentCode != "success" ||
            consent is null)
        {
            return Denied(consentCode);
        }

        return new AuthorizationResult(
            true,
            "success",
            policy,
            consent);
    }

    public async Task<string> ValidatePinnedAsync(
        CvAiAnalysisRun run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var current = await ValidateAsync(
            run.ApplicationId,
            cancellationToken);

        if (!current.Allowed)
            return current.Code;

        if (current.Policy is null ||
            current.Consent is null)
        {
            return "invalid_ai_authorization";
        }

        if (current.Policy.Id !=
                run.ProcessingPolicyId)
        {
            return "ai_processing_policy_changed";
        }

        if (current.Consent.Id !=
                run.ApplicationConsentId)
        {
            return "ai_consent_changed";
        }

        if (!string.Equals(
                current.Policy.ProviderName,
                run.Provider,
                StringComparison.Ordinal))
        {
            return "ai_provider_changed";
        }

        return "success";
    }
}
