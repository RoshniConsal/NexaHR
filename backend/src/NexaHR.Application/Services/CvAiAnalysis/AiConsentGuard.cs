using System.Globalization;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Application.Services.CvAiAnalysis;

public static class AiConsentGuard
{
    public static string GetExpectedTextVersion(
        ProcessingPolicy policy)
    {
        return "ai-consent-v1-policy-" +
            policy.Version.ToString(
                CultureInfo.InvariantCulture);
    }

    public static string ValidatePolicy(
        ProcessingPolicy? policy,
        string configuredProvider,
        Guid? approvedPolicyId,
        DateTime nowUtc)
    {
        if (policy is null)
            return "processing_policy_required";

        if (approvedPolicyId is null ||
            approvedPolicyId == Guid.Empty ||
            policy.Id != approvedPolicyId.Value)
        {
            return "ai_policy_not_enabled";
        }

        if (string.IsNullOrWhiteSpace(configuredProvider) ||
            !string.Equals(
                policy.ProviderName,
                configuredProvider,
                StringComparison.Ordinal))
        {
            return "ai_provider_not_approved";
        }

        if (policy.Version <= 0 ||
            policy.ApprovedByUserId == Guid.Empty ||
            policy.ApprovedAtUtc == default ||
            policy.ApprovedAtUtc > nowUtc ||
            string.IsNullOrWhiteSpace(
                policy.ApprovalReference))
        {
            return "ai_policy_not_approved";
        }

        if (policy.ProviderTrainingAllowed)
            return "ai_provider_training_not_permitted";

        if (policy.CandidateRetentionDays <= 0 ||
            string.IsNullOrWhiteSpace(
                policy.CandidateRetentionAction) ||
            string.IsNullOrWhiteSpace(
                policy.ProcessingPurpose) ||
            string.IsNullOrWhiteSpace(
                policy.DataCategories) ||
            string.IsNullOrWhiteSpace(
                policy.ProviderRetention) ||
            string.IsNullOrWhiteSpace(
                policy.ProviderAccessPolicy) ||
            string.IsNullOrWhiteSpace(
                policy.ProcessingLocation))
        {
            return "ai_policy_incomplete";
        }

        return "success";
    }

    public static bool IsEligibleApplicationStage(
        string stage)
    {
        return stage is
            ApplicationStatuses.Applied or
            ApplicationStatuses.UnderReview or
            ApplicationStatuses.Shortlisted or
            ApplicationStatuses.Interview or
            ApplicationStatuses.OfferSent or
            ApplicationStatuses.OfferAccepted;
    }

    public static string ValidateConsent(
        CandidateApplication application,
        ProcessingPolicy policy,
        ApplicationConsent? latestConsent,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(policy);

        if (!IsEligibleApplicationStage(
                application.Stage))
        {
            return "application_not_eligible_for_ai";
        }

        if (latestConsent is null)
            return "ai_consent_required";

        if (latestConsent.Id == Guid.Empty ||
            latestConsent.ApplicationId != application.Id ||
            latestConsent.ActorUserId !=
                application.CandidateUserId ||
            latestConsent.Purpose !=
                ApplicationConsentPurposes.AiProcessing ||
            !string.Equals(
                latestConsent.Provider,
                policy.ProviderName,
                StringComparison.Ordinal))
        {
            return "invalid_ai_consent";
        }

        if (latestConsent.AtUtc == default ||
            latestConsent.AtUtc > nowUtc ||
            latestConsent.AtUtc < application.AppliedAtUtc)
        {
            return "invalid_ai_consent_timestamp";
        }

        if (!latestConsent.Granted)
            return "ai_consent_revoked";

        if (latestConsent.ProcessingPolicyId != policy.Id ||
            latestConsent.TextVersion !=
                GetExpectedTextVersion(policy) ||
            latestConsent.AtUtc < policy.ApprovedAtUtc)
        {
            return "ai_consent_outdated";
        }

        if (string.IsNullOrWhiteSpace(
                latestConsent.ConsentText))
        {
            return "invalid_ai_consent";
        }

        return "success";
    }
}
