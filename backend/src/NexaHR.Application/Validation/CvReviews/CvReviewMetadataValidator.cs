using System.Text;
using NexaHR.Application.DTOs.CvReviews;

namespace NexaHR.Application.Validation.CvReviews;

public static class CvReviewMetadataValidator
{
    public const int MaxEvidenceReferences = 500;

    public const int MaxResolutionReasonLength = 1000;

    public const int MaxChangedFields = 8;

    public const string NoDuplicate = "NoDuplicate";

    public const string Acknowledged = "Acknowledged";

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    private static readonly HashSet<string> ScalarFields =
        new(StringComparer.Ordinal)
        {
            "fullName",
            "email",
            "phoneNumber"
        };

    private static readonly HashSet<string> EducationFields =
        new(StringComparer.Ordinal)
        {
            "education.institution",
            "education.qualification",
            "education.fieldOfStudy",
            "education.startYear",
            "education.endYear",
            "education.isOngoing"
        };

    private static readonly HashSet<string> ExperienceFields =
        new(StringComparer.Ordinal)
        {
            "experience.employer",
            "experience.jobTitle",
            "experience.startYear",
            "experience.startMonth",
            "experience.endYear",
            "experience.endMonth",
            "experience.isCurrent",
            "experience.description"
        };

    private static readonly HashSet<string> ChangedFields =
        new(StringComparer.Ordinal)
        {
            "fullName",
            "email",
            "phoneNumber",
            "skills",
            "education",
            "experience",
            "duplicateResolution.email",
            "duplicateResolution.phone"
        };

    public static IReadOnlyList<CvFieldValidationError>
        ValidateEvidence(
            CvStructuredValues? values,
            IReadOnlyList<CvFieldEvidence>? evidence)
    {
        var errors = new List<CvFieldValidationError>();

        if (values is null)
        {
            errors.Add(new("values", "required"));

            return errors.AsReadOnly();
        }

        // Evidence must refer to structurally valid values.
        if (CvStructuredValuesValidator
                .ValidateDraft(values).Count != 0)
        {
            errors.Add(new("values", "invalid_values"));

            return errors.AsReadOnly();
        }

        // Evidence is optional when there is no extraction.
        if (evidence is null)
        {
            return errors.AsReadOnly();
        }

        if (evidence.Count > MaxEvidenceReferences)
        {
            errors.Add(new(
                "evidence",
                "too_many_items"));

            return errors.AsReadOnly();
        }

        var seen = new HashSet<
            (string, int?, int?, int?)>();

        for (var index = 0;
             index < evidence.Count;
             index++)
        {
            var field = $"evidence[{index}]";

            var item = evidence[index];

            if (item is null)
            {
                errors.Add(new(field, "null_item"));

                continue;
            }

            if (!IsEvidenceFieldAllowed(item.Field))
            {
                errors.Add(new(
                    $"{field}.field",
                    "unsupported_field"));

                continue;
            }

            if (!HasReferencedValue(values, item))
            {
                errors.Add(new(
                    field,
                    "invalid_value_reference"));
            }

            // Exactly one positive source location.
            var hasPage = item.PageNumber.HasValue;

            var hasParagraph =
                item.ParagraphNumber.HasValue;

            if (hasPage == hasParagraph ||
                item.PageNumber is <= 0 ||
                item.ParagraphNumber is <= 0)
            {
                errors.Add(new(
                    field,
                    "invalid_source_location"));
            }

            if (!seen.Add((
                    item.Field,
                    item.ItemIndex,
                    item.PageNumber,
                    item.ParagraphNumber)))
            {
                errors.Add(new(
                    field,
                    "duplicate_reference"));
            }
        }

        return errors.AsReadOnly();
    }

    private static bool IsEvidenceFieldAllowed(
        string? field)
    {
        return field is not null &&
            (ScalarFields.Contains(field) ||
             field == "skills" ||
             EducationFields.Contains(field) ||
             ExperienceFields.Contains(field));
    }

    private static bool HasReferencedValue(
        CvStructuredValues values,
        CvFieldEvidence evidence)
    {
        if (ScalarFields.Contains(evidence.Field))
        {
            if (evidence.ItemIndex.HasValue)
            {
                return false;
            }

            return evidence.Field switch
            {
                "fullName" => HasText(values.FullName),

                "email" => HasText(values.Email),

                "phoneNumber" =>
                    HasText(values.PhoneNumber),

                _ => false
            };
        }

        if (!evidence.ItemIndex.HasValue ||
            evidence.ItemIndex.Value < 0)
        {
            return false;
        }

        var index = evidence.ItemIndex.Value;

        if (evidence.Field == "skills")
        {
            return index < values.Skills.Count &&
                   HasText(values.Skills[index]);
        }

        if (EducationFields.Contains(evidence.Field))
        {
            if (index >= values.Education.Count)
            {
                return false;
            }

            var item = values.Education[index];

            if (item is null)
            {
                return false;
            }

            return evidence.Field switch
            {
                "education.institution" =>
                    HasText(item.Institution),

                "education.qualification" =>
                    HasText(item.Qualification),

                "education.fieldOfStudy" =>
                    HasText(item.FieldOfStudy),

                "education.startYear" =>
                    item.StartYear.HasValue,

                "education.endYear" =>
                    item.EndYear.HasValue,

                "education.isOngoing" =>
                    item.IsOngoing.HasValue,

                _ => false
            };
        }

        if (ExperienceFields.Contains(evidence.Field))
        {
            if (index >= values.Experience.Count)
            {
                return false;
            }

            var item = values.Experience[index];

            if (item is null)
            {
                return false;
            }

            return evidence.Field switch
            {
                "experience.employer" =>
                    HasText(item.Employer),

                "experience.jobTitle" =>
                    HasText(item.JobTitle),

                "experience.startYear" =>
                    item.StartYear.HasValue,

                "experience.startMonth" =>
                    item.StartMonth.HasValue,

                "experience.endYear" =>
                    item.EndYear.HasValue,

                "experience.endMonth" =>
                    item.EndMonth.HasValue,

                "experience.isCurrent" =>
                    item.IsCurrent.HasValue,

                "experience.description" =>
                    HasText(item.Description),

                _ => false
            };
        }

        return false;
    }

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static IReadOnlyList<CvFieldValidationError>
        ValidateDuplicateDraft(
            CvDuplicateResolutionValues? resolution)
    {
        var errors = new List<CvFieldValidationError>();

        if (resolution is null)
        {
            errors.Add(new(
                "duplicateResolution",
                "required"));

            return errors.AsReadOnly();
        }

        ValidateResolution(
            errors,
            "email",
            resolution.EmailResolution,
            resolution.EmailReason,
            null);

        ValidateResolution(
            errors,
            "phone",
            resolution.PhoneResolution,
            resolution.PhoneReason,
            null);

        return errors.AsReadOnly();
    }

    // Duplicate flags must be obtained by the service
    // from a fresh database recheck, not from the client.
    public static IReadOnlyList<CvFieldValidationError>
        ValidateDuplicateConfirmation(
            CvDuplicateResolutionValues? resolution,
            bool duplicateEmail,
            bool duplicatePhone)
    {
        var errors = new List<CvFieldValidationError>();

        if (resolution is null)
        {
            errors.Add(new(
                "duplicateResolution",
                "required"));

            return errors.AsReadOnly();
        }

        ValidateResolution(
            errors,
            "email",
            resolution.EmailResolution,
            resolution.EmailReason,
            duplicateEmail);

        ValidateResolution(
            errors,
            "phone",
            resolution.PhoneResolution,
            resolution.PhoneReason,
            duplicatePhone);

        return errors.AsReadOnly();
    }

    private static void ValidateResolution(
        List<CvFieldValidationError> errors,
        string contact,
        string? resolution,
        string? reason,
        bool? duplicateExists)
    {
        var resolutionField =
            $"duplicateResolution.{contact}Resolution";

        var reasonField =
            $"duplicateResolution.{contact}Reason";

        if (resolution is not null &&
            resolution != NoDuplicate &&
            resolution != Acknowledged)
        {
            errors.Add(new(
                resolutionField,
                "unsupported_resolution"));
        }

        if (reason is not null &&
            !IsValidReason(reason))
        {
            errors.Add(new(
                reasonField,
                "invalid_reason"));
        }

        if (resolution == Acknowledged &&
            string.IsNullOrWhiteSpace(reason))
        {
            errors.Add(new(
                reasonField,
                "required"));
        }

        if (resolution != Acknowledged &&
            reason is not null)
        {
            errors.Add(new(
                reasonField,
                "unexpected_reason"));
        }

        // Draft decisions are provisional.
        if (!duplicateExists.HasValue)
        {
            return;
        }

        // Confirmation requires an explicit outcome.
        var expected = duplicateExists.Value
            ? Acknowledged
            : NoDuplicate;

        if (!string.Equals(
                resolution,
                expected,
                StringComparison.Ordinal))
        {
            errors.Add(new(
                resolutionField,
                "duplicate_resolution_mismatch"));
        }
    }

    private static bool IsValidReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Length > MaxResolutionReasonLength ||
            reason != reason.Trim() ||
            reason.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            StrictUtf8.GetByteCount(reason);

            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    // Validate system-generated change history.
    // Never trust client-supplied change tracking.
    public static IReadOnlyList<CvFieldValidationError>
        ValidateChangedFields(
            IReadOnlyList<string>? changedFields)
    {
        var errors = new List<CvFieldValidationError>();

        if (changedFields is null)
        {
            errors.Add(new(
                "changedFields",
                "null_collection"));

            return errors.AsReadOnly();
        }

        if (changedFields.Count > MaxChangedFields)
        {
            errors.Add(new(
                "changedFields",
                "too_many_items"));

            return errors.AsReadOnly();
        }

        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        for (var index = 0;
             index < changedFields.Count;
             index++)
        {
            var item = changedFields[index];

            var field = $"changedFields[{index}]";

            if (item is null ||
                !ChangedFields.Contains(item))
            {
                errors.Add(new(
                    field,
                    "unsupported_field"));

                continue;
            }

            if (!seen.Add(item))
            {
                errors.Add(new(
                    field,
                    "duplicate_field"));
            }
        }

        return errors.AsReadOnly();
    }
}
