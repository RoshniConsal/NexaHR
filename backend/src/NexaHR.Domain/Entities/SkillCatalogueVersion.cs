namespace NexaHR.Domain.Entities;

public sealed class SkillCatalogueVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Version { get; set; }

    public Guid? PreviousVersionId { get; set; }

    public string CatalogueJson { get; set; } = "[]";

    public string ContentSha256Hash { get; set; }
        = string.Empty;

    public string ReviewReason { get; set; }
        = string.Empty;

    public Guid PublishedByUserId { get; set; }

    public DateTime PublishedAtUtc { get; set; }
        = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = [];
}
