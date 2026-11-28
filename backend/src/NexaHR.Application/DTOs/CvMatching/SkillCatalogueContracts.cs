using System.ComponentModel.DataAnnotations;
using NexaHR.Application.Services.CvMatching;

namespace NexaHR.Application.DTOs.CvMatching;

public sealed class PublishSkillCatalogueRequest
{
    [Range(0, int.MaxValue)]
    public int ExpectedVersion { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string ReviewReason { get; init; } = "";

    [Required]
    [MaxLength(500)]
    public List<SkillCatalogueEntry> Entries { get; init; }
        = [];
}

public sealed class SkillCatalogueVersionSummary
{
    public Guid Id { get; init; }

    public int Version { get; init; }

    public Guid? PreviousVersionId { get; init; }

    public string ContentSha256Hash { get; init; } = "";

    public Guid PublishedByUserId { get; init; }

    public DateTime PublishedAtUtc { get; init; }
}

public sealed class SkillCatalogueResponse
{
    public Guid Id { get; init; }

    public int Version { get; init; }

    public Guid? PreviousVersionId { get; init; }

    public string ContentSha256Hash { get; init; } = "";

    public string ReviewReason { get; init; } = "";

    public Guid PublishedByUserId { get; init; }

    public DateTime PublishedAtUtc { get; init; }

    public IReadOnlyList<SkillCatalogueEntry> Entries
        { get; init; } = [];

    public IReadOnlyList<SkillCatalogueVersionSummary>
        VersionIndex { get; init; } = [];

    public bool CanPublish { get; init; }
}
