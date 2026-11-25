namespace NexaHR.Domain.Entities;

public sealed class RetentionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessingPolicyId { get; set; }

    public Guid AuthorizedByUserId { get; set; }

    public string DataCategory { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string AuthorizationReason { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int MaxRecords { get; set; }

    public int EligibleCount { get; set; }

    public int ProcessedCount { get; set; }

    public int HeldCount { get; set; }

    public int FailedCount { get; set; }

    public DateTime CutoffUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? ResultCode { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
