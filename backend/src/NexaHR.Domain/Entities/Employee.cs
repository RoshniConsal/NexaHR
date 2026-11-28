namespace NexaHR.Domain.Entities;

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public DateOnly JoiningDate { get; set; }

    public string? WorkEmail { get; set; }

    public string? NormalizedWorkEmail { get; set; }

    public DateTime? WorkEmailVerifiedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
