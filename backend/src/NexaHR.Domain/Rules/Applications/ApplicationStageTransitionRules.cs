using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Rules.Applications;

public static class ApplicationStageTransitionRules
{
    public static bool IsSupportedTarget(string? targetStage)
    {
        return targetStage is
            ApplicationStatuses.UnderReview or
            ApplicationStatuses.Shortlisted or
            ApplicationStatuses.Rejected;
    }

    public static bool IsAllowed(
        string? currentStage,
        string? targetStage)
    {
        return (currentStage, targetStage) switch
        {
            (
                ApplicationStatuses.Applied,
                ApplicationStatuses.UnderReview
            ) => true,

            (
                ApplicationStatuses.UnderReview,
                ApplicationStatuses.Shortlisted
            ) => true,

            (
                ApplicationStatuses.UnderReview,
                ApplicationStatuses.Rejected
            ) => true,

            (
                ApplicationStatuses.Shortlisted,
                ApplicationStatuses.UnderReview
            ) => true,

            (
                ApplicationStatuses.Shortlisted,
                ApplicationStatuses.Rejected
            ) => true,

            _ => false
        };
    }

    public static bool RequiresConfirmedCvReview(
        string? currentStage,
        string? targetStage)
    {
        return IsAllowed(currentStage, targetStage) &&
            (
                (
                    currentStage == ApplicationStatuses.Applied &&
                    targetStage == ApplicationStatuses.UnderReview
                ) ||
                targetStage == ApplicationStatuses.Shortlisted
            );
    }

    public static bool RequiresReason(string? targetStage)
    {
        return targetStage == ApplicationStatuses.Rejected;
    }
}
