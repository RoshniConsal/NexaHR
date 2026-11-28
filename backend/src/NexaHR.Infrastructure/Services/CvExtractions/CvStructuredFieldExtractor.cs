using System.Text.RegularExpressions;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Validation.CvReviews;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed record CvStructuredExtractionResult(
    CvStructuredValues Values,
    IReadOnlyList<CvFieldEvidence> Evidence);

public sealed class CvStructuredFieldExtractor
{
    private static readonly TimeSpan RegexTimeout =
        TimeSpan.FromSeconds(1);

    private static readonly Regex EmailPattern = new(
        @"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PhonePattern = new(
        @"(?<![\w])(?:\+94[ -]?|0)(?:[0-9][ -]?){8}[0-9](?![\w])",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly HashSet<string> SkillHeadings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Skills",
            "Technical Skills",
            "Core Skills",
            "Professional Skills",
            "Technical Competencies",
            "Technologies"
        };

    public CvStructuredExtractionResult Extract(
        IReadOnlyList<CvTextSegment> segments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(segments);

        string? email = null;
        string? phone = null;

        var skills = new List<string>();

        var seenSkills = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var evidence = new List<CvFieldEvidence>();

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (email is null)
            {
                foreach (Match match in
                         EmailPattern.Matches(segment.Text))
                {
                    var candidate = match.Value;

                    var probe = new CvStructuredValues
                    {
                        Email = candidate
                    };

                    if (CvStructuredValuesValidator
                            .ValidateDraft(probe).Count != 0)
                        continue;

                    email = candidate;

                    evidence.Add(CreateEvidence(
                        "email", null, segment));

                    break;
                }
            }

            if (phone is null)
            {
                foreach (Match match in
                         PhonePattern.Matches(segment.Text))
                {
                    var candidate = match.Value.Trim();

                    var probe = new CvStructuredValues
                    {
                        PhoneNumber = candidate
                    };

                    if (CvStructuredValuesValidator
                            .ValidateDraft(probe).Count != 0)
                        continue;

                    phone = candidate;

                    evidence.Add(CreateEvidence(
                        "phoneNumber", null, segment));

                    break;
                }
            }

            foreach (var rawLine in
                     segment.Text.Split('\n'))
            {
                var line = rawLine.Trim();

                var colon = line.IndexOf(':');

                if (colon <= 0)
                    continue;

                var heading = line[..colon].Trim();

                if (!SkillHeadings.Contains(heading))
                    continue;

                var content = line[(colon + 1)..];

                foreach (var rawSkill in content.Split(
                             new[] { ',', ';', '|' },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var skill = rawSkill.Trim()
                        .TrimStart('•', '-', '–')
                        .Trim();

                    if (skills.Count >=
                        CvStructuredValuesValidator.MaxSkills)
                        break;

                    if (string.IsNullOrWhiteSpace(skill) ||
                        skill.Length >
                        CvStructuredValuesValidator.MaxSkillLength ||
                        skill.Any(char.IsControl))
                        continue;

                    var probe = new CvStructuredValues
                    {
                        Skills = new[] { skill }
                    };

                    if (CvStructuredValuesValidator
                            .ValidateDraft(probe).Count != 0)
                        continue;

                    if (!seenSkills.Add(skill))
                        continue;

                    var index = skills.Count;

                    skills.Add(skill);

                    evidence.Add(CreateEvidence(
                        "skills", index, segment));
                }
            }
        }

        var values = new CvStructuredValues
        {
            Email = email,
            PhoneNumber = phone,
            Skills = skills
        };

        if (CvStructuredValuesValidator
                .ValidateDraft(values).Count != 0)
        {
            throw new InvalidDataException(
                "Extracted CV values are invalid.");
        }

        if (CvReviewMetadataValidator
                .ValidateEvidence(values, evidence).Count != 0)
        {
            throw new InvalidDataException(
                "Extracted CV evidence is invalid.");
        }

        return new CvStructuredExtractionResult(
            values,
            evidence.AsReadOnly());
    }

    private static CvFieldEvidence CreateEvidence(
        string field,
        int? itemIndex,
        CvTextSegment segment)
    {
        return new CvFieldEvidence
        {
            Field = field,
            ItemIndex = itemIndex,
            PageNumber = segment.PageNumber,
            ParagraphNumber = segment.ParagraphNumber
        };
    }
}
