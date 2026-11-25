using NexaHR.Domain.Constants;

namespace NexaHR.Domain.Entities;

public sealed class Vacancy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DepartmentId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string Status { get; set; } = VacancyStatuses.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? PublishedRevisionId { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public string? ClosureReason { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
