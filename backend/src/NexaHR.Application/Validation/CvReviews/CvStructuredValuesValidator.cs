using System.Net.Mail;
using System.Text;
using NexaHR.Application.DTOs.CvReviews;

namespace NexaHR.Application.Validation.CvReviews;

public sealed record CvFieldValidationError(
    string Field,
    string Code);

public static class CvStructuredValuesValidator
{
    public const int MaxFullNameLength = 200;
    public const int MaxEmailLength = 254;
    public const int MaxPhoneLength = 32;

    public const int MaxSkills = 100;
    public const int MaxSkillLength = 100;

    public const int MaxEducationRecords = 30;
    public const int MaxExperienceRecords = 50;

    public const int MaxDescriptionLength = 2000;

    private const int MinYear = 1900;

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    public static IReadOnlyList<CvFieldValidationError>
        ValidateDraft(CvStructuredValues? values)
    {
        return Validate(values, false);
    }

    public static IReadOnlyList<CvFieldValidationError>
        ValidateConfirmation(CvStructuredValues? values)
    {
        return Validate(values, true);
    }

    private static IReadOnlyList<CvFieldValidationError>
        Validate(
            CvStructuredValues? values,
            bool confirmation)
    {
        var errors = new List<CvFieldValidationError>();

        if (values is null)
        {
            errors.Add(new("values", "required"));
            return errors.AsReadOnly();
        }

        if (values.SchemaVersion !=
            CvReviewJsonReader.SupportedSchemaVersion)
        {
            errors.Add(new(
                "schemaVersion",
                "unsupported_version"));
        }

        CheckText(
            errors,
            "fullName",
            values.FullName,
            MaxFullNameLength,
            confirmation);

        CheckText(
            errors,
            "email",
            values.Email,
            MaxEmailLength,
            confirmation);

        if (!string.IsNullOrWhiteSpace(values.Email) &&
            !IsValidEmail(values.Email))
        {
            errors.Add(new("email", "invalid_email"));
        }

        CheckText(
            errors,
            "phoneNumber",
            values.PhoneNumber,
            MaxPhoneLength);

        if (!string.IsNullOrWhiteSpace(values.PhoneNumber) &&
            !IsValidPhone(values.PhoneNumber))
        {
            errors.Add(new(
                "phoneNumber",
                "invalid_phone"));
        }

        ValidateSkills(errors, values.Skills);

        var currentYear = DateTime.UtcNow.Year;

        ValidateEducation(
            errors,
            values.Education,
            currentYear);

        ValidateExperience(
            errors,
            values.Experience,
            currentYear);

        return errors.AsReadOnly();
    }

    private static void CheckText(
        List<CvFieldValidationError> errors,
        string field,
        string? value,
        int maxLength,
        bool required = false,
        bool multiline = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(new(field, "required"));
            }

            // Unknown optional values should be null.
            if (value is not null)
            {
                errors.Add(new(field, "blank_value"));
            }

            return;
        }

        if (value.Length > maxLength)
        {
            errors.Add(new(field, "too_long"));
            return;
        }

        try
        {
            StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException)
        {
            errors.Add(new(field, "invalid_unicode"));
            return;
        }

        if (value != value.Trim())
        {
            errors.Add(new(
                field,
                "unexpected_whitespace"));
        }

        foreach (var character in value)
        {
            if (char.IsControl(character) &&
                !(multiline &&
                  character is '\r' or '\n' or '\t'))
            {
                errors.Add(new(
                    field,
                    "invalid_character"));

                break;
            }
        }
    }

    private static bool IsValidEmail(string value)
    {
        if (value.Length > MaxEmailLength ||
            value != value.Trim() ||
            value.Any(char.IsWhiteSpace) ||
            value.Any(char.IsControl))
        {
            return false;
        }

        if (!MailAddress.TryCreate(value, out var address) ||
            address is null)
        {
            return false;
        }

        return string.Equals(
                   address.Address,
                   value,
                   StringComparison.Ordinal) &&
               string.IsNullOrEmpty(address.DisplayName) &&
               address.Host.Contains('.') &&
               !address.Host.StartsWith('.') &&
               !address.Host.EndsWith('.') &&
               !address.Host.Contains("..");
    }

    private static bool IsValidPhone(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxPhoneLength ||
            value != value.Trim())
        {
            return false;
        }

        if (value[0] != '+' &&
            value[0] != '(' &&
            value[0] is not (>= '0' and <= '9'))
        {
            return false;
        }

        if (value[^1] != ')' &&
            value[^1] is not (>= '0' and <= '9'))
        {
            return false;
        }

        var digits = new StringBuilder();

        var parenthesesOpen = false;
        var groupDigits = 0;

        for (var index = 0;
             index < value.Length;
             index++)
        {
            var character = value[index];

            if (character is >= '0' and <= '9')
            {
                digits.Append(character);

                if (parenthesesOpen)
                {
                    groupDigits++;
                }

                continue;
            }

            switch (character)
            {
                case '+' when index == 0:
                    break;

                case ' ':
                case '-':
                    if (index == 0 ||
                        index == value.Length - 1 ||
                        value[index - 1] is ' ' or '-' or '(' or '+' ||
                        value[index + 1] is ' ' or '-' or ')')
                    {
                        return false;
                    }

                    break;

                case '(' when !parenthesesOpen:
                    parenthesesOpen = true;
                    groupDigits = 0;
                    break;

                case ')' when parenthesesOpen:
                    if (groupDigits == 0)
                    {
                        return false;
                    }

                    parenthesesOpen = false;
                    break;

                default:
                    return false;
            }
        }

        if (parenthesesOpen)
        {
            return false;
        }

        var normalized = digits.ToString();

        if (value.StartsWith('+'))
        {
            if (value.Length < 2 ||
                value[1] is < '1' or > '9')
            {
                return false;
            }
        }
        else if (normalized.StartsWith(
                     "00", StringComparison.Ordinal))
        {
            if (!value.StartsWith(
                    "00", StringComparison.Ordinal))
            {
                return false;
            }

            normalized = normalized[2..];

            if (normalized.Length == 0 ||
                normalized[0] == '0')
            {
                return false;
            }
        }

        // Keep the existing Sri Lankan local-number
        // normalization rule consistent with ContactNormalizer.
        if (normalized.Length == 10 &&
            normalized.StartsWith(
                "0", StringComparison.Ordinal))
        {
            normalized = "94" + normalized[1..];
        }

        return normalized.Length is >= 7 and <= 15;
    }

    private static void ValidateSkills(
        List<CvFieldValidationError> errors,
        IReadOnlyList<string>? skills)
    {
        if (skills is null)
        {
            errors.Add(new("skills", "null_collection"));
            return;
        }

        if (skills.Count > MaxSkills)
        {
            errors.Add(new("skills", "too_many_items"));
            return;
        }

        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0;
             index < skills.Count;
             index++)
        {
            var field = $"skills[{index}]";
            var skill = skills[index];

            CheckText(
                errors,
                field,
                skill,
                MaxSkillLength,
                required: true);

            if (!string.IsNullOrWhiteSpace(skill) &&
                !seen.Add(skill.Trim()))
            {
                errors.Add(new(
                    field,
                    "duplicate_skill"));
            }
        }
    }

    private static void ValidateEducation(
        List<CvFieldValidationError> errors,
        IReadOnlyList<CvEducationValue>? education,
        int currentYear)
    {
        if (education is null)
        {
            errors.Add(new(
                "education",
                "null_collection"));

            return;
        }

        if (education.Count > MaxEducationRecords)
        {
            errors.Add(new(
                "education",
                "too_many_items"));

            return;
        }

        for (var index = 0;
             index < education.Count;
             index++)
        {
            var field = $"education[{index}]";
            var item = education[index];

            if (item is null)
            {
                errors.Add(new(field, "null_item"));
                continue;
            }

            CheckText(
                errors,
                $"{field}.institution",
                item.Institution,
                250);

            CheckText(
                errors,
                $"{field}.qualification",
                item.Qualification,
                250);

            CheckText(
                errors,
                $"{field}.fieldOfStudy",
                item.FieldOfStudy,
                250);

            CheckYear(
                errors,
                $"{field}.startYear",
                item.StartYear,
                currentYear);

            CheckYear(
                errors,
                $"{field}.endYear",
                item.EndYear,
                currentYear);

            if (item.StartYear.HasValue &&
                item.EndYear.HasValue &&
                item.EndYear.Value <
                item.StartYear.Value)
            {
                errors.Add(new(
                    $"{field}.endYear",
                    "invalid_date_order"));
            }

            if (item.IsOngoing == true &&
                item.EndYear.HasValue)
            {
                errors.Add(new(
                    $"{field}.endYear",
                    "ongoing_has_end_date"));
            }
        }
    }

    private static void ValidateExperience(
        List<CvFieldValidationError> errors,
        IReadOnlyList<CvExperienceValue>? experience,
        int currentYear)
    {
        if (experience is null)
        {
            errors.Add(new(
                "experience",
                "null_collection"));

            return;
        }

        if (experience.Count > MaxExperienceRecords)
        {
            errors.Add(new(
                "experience",
                "too_many_items"));

            return;
        }

        for (var index = 0;
             index < experience.Count;
             index++)
        {
            var field = $"experience[{index}]";
            var item = experience[index];

            if (item is null)
            {
                errors.Add(new(field, "null_item"));
                continue;
            }

            CheckText(
                errors,
                $"{field}.employer",
                item.Employer,
                250);

            CheckText(
                errors,
                $"{field}.jobTitle",
                item.JobTitle,
                250);

            CheckText(
                errors,
                $"{field}.description",
                item.Description,
                MaxDescriptionLength,
                multiline: true);

            CheckYear(
                errors,
                $"{field}.startYear",
                item.StartYear,
                currentYear);

            CheckYear(
                errors,
                $"{field}.endYear",
                item.EndYear,
                currentYear);

            CheckMonth(
                errors,
                $"{field}.startMonth",
                item.StartMonth,
                item.StartYear);

            CheckMonth(
                errors,
                $"{field}.endMonth",
                item.EndMonth,
                item.EndYear);

            if (item.StartYear.HasValue &&
                item.EndYear.HasValue)
            {
                var invalidOrder =
                    item.EndYear.Value <
                    item.StartYear.Value;

                if (item.EndYear.Value ==
                    item.StartYear.Value &&
                    item.StartMonth.HasValue &&
                    item.EndMonth.HasValue)
                {
                    invalidOrder =
                        item.EndMonth.Value <
                        item.StartMonth.Value;
                }

                if (invalidOrder)
                {
                    errors.Add(new(
                        $"{field}.endYear",
                        "invalid_date_order"));
                }
            }

            if (item.IsCurrent == true &&
                (item.EndYear.HasValue ||
                 item.EndMonth.HasValue))
            {
                errors.Add(new(
                    $"{field}.endYear",
                    "current_has_end_date"));
            }
        }
    }

    private static void CheckYear(
        List<CvFieldValidationError> errors,
        string field,
        int? year,
        int currentYear)
    {
        if (year.HasValue &&
            (year.Value < MinYear ||
             year.Value > currentYear))
        {
            errors.Add(new(field, "invalid_year"));
        }
    }

    private static void CheckMonth(
        List<CvFieldValidationError> errors,
        string field,
        int? month,
        int? year)
    {
        if (!month.HasValue)
        {
            return;
        }

        if (month.Value < 1 ||
            month.Value > 12)
        {
            errors.Add(new(field, "invalid_month"));
        }

        if (!year.HasValue)
        {
            errors.Add(new(field, "year_required"));
        }
    }
}
