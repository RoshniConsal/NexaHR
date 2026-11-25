namespace NexaHR.Domain.Entities;

public sealed class VacancyDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VacancyRevisionId { get; set; }

    public Guid ApproverUserId { get; set; }

    public string ApproverRole { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
