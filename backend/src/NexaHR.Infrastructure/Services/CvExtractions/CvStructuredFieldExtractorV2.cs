using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Validation.CvReviews;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed class CvStructuredFieldExtractorV2
{
    public const string ParserVersion = "nexahr-text-v2";

    public CvStructuredExtractionResult Extract(
        IReadOnlyList<CvTextSegment> segments,
        CancellationToken cancellationToken)
    {
        var baseline = new CvStructuredFieldExtractor()
            .Extract(segments, cancellationToken);

        var original = baseline.Values;

        string? fullName = original.FullName;

        var education = original.Education.ToList();
        var experience = original.Experience.ToList();
        var evidence = baseline.Evidence.ToList();

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var rawLine in segment.Text.Split('\n'))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = rawLine.Trim();

                var separator = line.IndexOf(':');

                if (separator <= 0)
                    continue;

                var label = line[..separator]
                    .Trim()
                    .ToLowerInvariant();

                var value = line[(separator + 1)..].Trim();

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (evidence.Count >=
                    CvReviewMetadataValidator.MaxEvidenceReferences)
                    break;

                if (label is "full name" or "name")
                {
                    if (fullName is not null)
                        continue;

                    if (!value.Any(char.IsLetter) ||
                        value.Any(char.IsDigit) ||
                        value.Contains('@'))
                        continue;

                    var candidate = new CvStructuredValues
                    {
                        FullName = value
                    };

                    if (CvStructuredValuesValidator
                        .ValidateDraft(candidate).Count != 0)
                        continue;

                    fullName = value;

                    evidence.Add(CreateEvidence(
                        "fullName", null, segment));

                    continue;
                }

                if (label is "education" or "qualification"
                    or "degree" or "academic qualification")
                {
                    if (education.Count >=
                        CvStructuredValuesValidator.MaxEducationRecords)
                        continue;

                    if (education.Any(e =>
                        string.Equals(
                            e.Qualification,
                            value,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var item = new CvEducationValue
                    {
                        Qualification = value
                    };

                    var candidate = new CvStructuredValues
                    {
                        Education = [item]
                    };

                    if (CvStructuredValuesValidator
                        .ValidateDraft(candidate).Count != 0)
                        continue;

                    var index = education.Count;

                    education.Add(item);

                    evidence.Add(CreateEvidence(
                        "education.qualification",
                        index,
                        segment));

                    continue;
                }

                if (label is "experience" or "work experience"
                    or "job title" or "position")
                {
                    if (experience.Count >=
                        CvStructuredValuesValidator.MaxExperienceRecords)
                        continue;

                    // Only explicit job-title labels are mapped
                    // directly. Ambiguous experience descriptions
                    // are left for HR review.
                    if (label is "experience" or "work experience")
                        continue;

                    if (experience.Any(e =>
                        string.Equals(
                            e.JobTitle,
                            value,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var item = new CvExperienceValue
                    {
                        JobTitle = value
                    };

                    var candidate = new CvStructuredValues
                    {
                        Experience = [item]
                    };

                    if (CvStructuredValuesValidator
                        .ValidateDraft(candidate).Count != 0)
                        continue;

                    var index = experience.Count;

                    experience.Add(item);

                    evidence.Add(CreateEvidence(
                        "experience.jobTitle",
                        index,
                        segment));
                }
            }
        }

        var values = new CvStructuredValues
        {
            SchemaVersion = original.SchemaVersion,
            FullName = fullName,
            Email = original.Email,
            PhoneNumber = original.PhoneNumber,
            Skills = original.Skills,
            Education = education,
            Experience = experience
        };

        if (CvStructuredValuesValidator
            .ValidateDraft(values).Count != 0)
        {
            throw new InvalidDataException(
                "Invalid extracted values.");
        }

        if (CvReviewMetadataValidator
            .ValidateEvidence(values, evidence).Count != 0)
        {
            throw new InvalidDataException(
                "Invalid extraction evidence.");
        }

        return new CvStructuredExtractionResult(
            values, evidence);
    }

    private static CvFieldEvidence CreateEvidence(
        string field,
        int? index,
        CvTextSegment segment)
    {
        return new CvFieldEvidence
        {
            Field = field,
            ItemIndex = index,
            PageNumber = segment.PageNumber,
            ParagraphNumber = segment.ParagraphNumber
        };
    }
}
