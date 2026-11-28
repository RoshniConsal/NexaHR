using NexaHR.Application.DTOs.CvReviews;

namespace NexaHR.Application.Validation.CvReviews;

public sealed record CvReviewChangeResult(
    IReadOnlyList<string> ChangedFields,
    bool HasMaterialChanges);

public static class CvReviewChangeTracker
{
    public static CvReviewChangeResult Compare(
        CvStructuredValues? previousValues,
        CvDuplicateResolutionValues? previousResolution,
        CvStructuredValues currentValues,
        CvDuplicateResolutionValues currentResolution)
    {
        ArgumentNullException.ThrowIfNull(currentValues);
        ArgumentNullException.ThrowIfNull(currentResolution);

        EnsureValid(
            currentValues,
            currentResolution);

        // A previous review must contain both components.
        if ((previousValues is null) !=
            (previousResolution is null))
        {
            throw new InvalidOperationException(
                "Previous CV review data is incomplete.");
        }

        if (previousValues is not null &&
            previousResolution is not null)
        {
            EnsureValid(
                previousValues,
                previousResolution);
        }

        // A first review is compared against an empty
        // structured baseline. No values are fabricated.
        var baselineValues =
            previousValues ?? new CvStructuredValues();

        var baselineResolution =
            previousResolution ??
            new CvDuplicateResolutionValues();

        var changed = new List<string>();

        if (!EqualText(
                baselineValues.FullName,
                currentValues.FullName))
        {
            changed.Add("fullName");
        }

        if (!EqualText(
                baselineValues.Email,
                currentValues.Email))
        {
            changed.Add("email");
        }

        if (!EqualText(
                baselineValues.PhoneNumber,
                currentValues.PhoneNumber))
        {
            changed.Add("phoneNumber");
        }

        if (!baselineValues.Skills.SequenceEqual(
                currentValues.Skills,
                StringComparer.Ordinal))
        {
            changed.Add("skills");
        }

        if (!EqualEducation(
                baselineValues.Education,
                currentValues.Education))
        {
            changed.Add("education");
        }

        if (!EqualExperience(
                baselineValues.Experience,
                currentValues.Experience))
        {
            changed.Add("experience");
        }

        if (!EqualText(
                baselineResolution.EmailResolution,
                currentResolution.EmailResolution) ||
            !EqualText(
                baselineResolution.EmailReason,
                currentResolution.EmailReason))
        {
            changed.Add("duplicateResolution.email");
        }

        if (!EqualText(
                baselineResolution.PhoneResolution,
                currentResolution.PhoneResolution) ||
            !EqualText(
                baselineResolution.PhoneReason,
                currentResolution.PhoneReason))
        {
            changed.Add("duplicateResolution.phone");
        }

        if (CvReviewMetadataValidator
                .ValidateChangedFields(changed).Count != 0)
        {
            throw new InvalidOperationException(
                "Generated CV change history is invalid.");
        }

        // Conservative policy: changes to reviewed
        // values or duplicate decisions are material.
        return new CvReviewChangeResult(
            changed.AsReadOnly(),
            changed.Count != 0);
    }

    private static void EnsureValid(
        CvStructuredValues values,
        CvDuplicateResolutionValues resolution)
    {
        if (CvStructuredValuesValidator
                .ValidateDraft(values).Count != 0 ||
            CvReviewMetadataValidator
                .ValidateDuplicateDraft(resolution)
                .Count != 0)
        {
            throw new InvalidOperationException(
                "CV review comparison data is invalid.");
        }
    }

    private static bool EqualText(
        string? left,
        string? right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.Ordinal);
    }

    private static bool EqualEducation(
        IReadOnlyList<CvEducationValue> left,
        IReadOnlyList<CvEducationValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0;
             index < left.Count;
             index++)
        {
            var a = left[index];
            var b = right[index];

            if (!EqualText(
                    a.Institution,
                    b.Institution) ||
                !EqualText(
                    a.Qualification,
                    b.Qualification) ||
                !EqualText(
                    a.FieldOfStudy,
                    b.FieldOfStudy) ||
                a.StartYear != b.StartYear ||
                a.EndYear != b.EndYear ||
                a.IsOngoing != b.IsOngoing)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualExperience(
        IReadOnlyList<CvExperienceValue> left,
        IReadOnlyList<CvExperienceValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0;
             index < left.Count;
             index++)
        {
            var a = left[index];
            var b = right[index];

            if (!EqualText(
                    a.Employer,
                    b.Employer) ||
                !EqualText(
                    a.JobTitle,
                    b.JobTitle) ||
                a.StartYear != b.StartYear ||
                a.StartMonth != b.StartMonth ||
                a.EndYear != b.EndYear ||
                a.EndMonth != b.EndMonth ||
                a.IsCurrent != b.IsCurrent ||
                !EqualText(
                    a.Description,
                    b.Description))
            {
                return false;
            }
        }

        return true;
    }
}
