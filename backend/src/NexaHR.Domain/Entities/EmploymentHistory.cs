namespace NexaHR.Domain.Entities;

public sealed class EmploymentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmployeeId { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string? PreviousValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
