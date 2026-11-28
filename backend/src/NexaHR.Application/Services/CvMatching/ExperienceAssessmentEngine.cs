using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Application.Services.CvMatching;

public sealed record ExperienceAssessmentResult(
    decimal? RequiredYears,
    int EvidencedMonths,
    decimal EvidencedYears,
    int CompleteRecords,
    int IncompleteRecords,
    string Status,
    DateOnly AssessmentDate);

public sealed record ManualReviewFlag(
    Guid CriterionId,
    string Label,
    string Status);

public sealed class ExperienceAssessmentEngine
{
    public ExperienceAssessmentResult Assess(
        decimal? minimumYears,
        IReadOnlyList<CvExperienceValue> experience,
        DateOnly assessmentDate)
    {
        ArgumentNullException.ThrowIfNull(experience);

        if (minimumYears is < 0)
            throw new ArgumentException(
                "Minimum experience cannot be negative.");

        var months = new HashSet<int>();

        var complete = 0;
        var incomplete = 0;

        var currentMonth =
            assessmentDate.Year * 12 +
            assessmentDate.Month - 1;

        foreach (var item in experience)
        {
            if (item.StartYear is not int startYear ||
                item.StartMonth is not int startMonth ||
                startYear < 1900 ||
                startMonth < 1 ||
                startMonth > 12)
            {
                incomplete++;
                continue;
            }

            var start = startYear * 12 +
                startMonth - 1;

            int end;

            if (item.IsCurrent == true)
            {
                end = currentMonth;
            }
            else
            {
                if (item.EndYear is not int endYear ||
                    item.EndMonth is not int endMonth ||
                    endYear < 1900 ||
                    endMonth < 1 ||
                    endMonth > 12)
                {
                    incomplete++;
                    continue;
                }

                end = endYear * 12 + endMonth - 1;
            }

            if (start > end ||
                start > currentMonth ||
                end > currentMonth)
            {
                incomplete++;
                continue;
            }

            complete++;

            // Count completed calendar-month intervals.
            // Overlapping employment is counted only once.
            for (var month = start; month < end; month++)
                months.Add(month);
        }

        var exactYears = months.Count / 12m;

        var evidencedYears = Math.Round(
            exactYears,
            2,
            MidpointRounding.AwayFromZero);

        string status;

        if (minimumYears is null || minimumYears == 0)
            status = "NotRequired";
        else if (exactYears >= minimumYears.Value)
            status = "MinimumEvidenced";
        else if (complete == 0 || incomplete > 0)
            status = "InsufficientEvidence";
        else
            status = "MinimumNotEvidenced";

        return new ExperienceAssessmentResult(
            minimumYears,
            months.Count,
            evidencedYears,
            complete,
            incomplete,
            status,
            assessmentDate);
    }

    public IReadOnlyList<ManualReviewFlag> GetReviewFlags(
        IReadOnlyList<VacancyCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return criteria
            .Where(c =>
                c.Kind == VacancyCriterionKinds.ReviewFlag)
            .OrderBy(c => c.Label, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .Select(c => new ManualReviewFlag(
                c.Id,
                c.Label,
                "RequiresHumanReview"))
            .ToList();
    }
}
