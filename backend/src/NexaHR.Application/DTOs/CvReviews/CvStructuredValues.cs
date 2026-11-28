using System.Text.Json.Serialization;

namespace NexaHR.Application.DTOs.CvReviews;

// Shared schema for extracted and manually reviewed values.
// Unknown values must remain null rather than being invented.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CvStructuredValues
{
    public int SchemaVersion { get; init; } = 1;

    public string? FullName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public IReadOnlyList<string> Skills { get; init; } = [];

    public IReadOnlyList<CvEducationValue> Education { get; init; }
        = [];

    public IReadOnlyList<CvExperienceValue> Experience { get; init; }
        = [];
}

// Education values must reflect the source or HR corrections.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CvEducationValue
{
    public string? Institution { get; init; }

    public string? Qualification { get; init; }

    public string? FieldOfStudy { get; init; }

    public int? StartYear { get; init; }

    public int? EndYear { get; init; }

    public bool? IsOngoing { get; init; }
}

// Partial dates remain partial; missing dates are not invented.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CvExperienceValue
{
    public string? Employer { get; init; }

    public string? JobTitle { get; init; }

    public int? StartYear { get; init; }

    public int? StartMonth { get; init; }

    public int? EndYear { get; init; }

    public int? EndMonth { get; init; }

    public bool? IsCurrent { get; init; }

    public string? Description { get; init; }
}

// Evidence identifies the actual source location.
// No raw CV text, storage path or parser diagnostics.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CvFieldEvidence
{
    public string Field { get; init; } = string.Empty;

    public int? ItemIndex { get; init; }

    public int? PageNumber { get; init; }

    public int? ParagraphNumber { get; init; }
}

// Separate HR duplicate-contact resolution.
// The service must independently recheck duplicate contacts.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CvDuplicateResolutionValues
{
    public string? EmailResolution { get; init; }

    public string? PhoneResolution { get; init; }

    public string? EmailReason { get; init; }

    public string? PhoneReason { get; init; }
}
