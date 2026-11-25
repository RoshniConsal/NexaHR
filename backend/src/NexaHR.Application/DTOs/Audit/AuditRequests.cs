using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Audit;

public sealed class AuditEventQuery
{
    [StringLength(50)]
    public string? Module { get; set; }

    [StringLength(100)]
    public string? Action { get; set; }

    [StringLength(30)]
    public string? Outcome { get; set; }

    public Guid? ActorUserId { get; set; }

    public Guid? EntityId { get; set; }

    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [RegularExpression("^(json|csv)$")]
    public string Format { get; set; } = "json";
}

// Internal service command.
// Must never be exposed as a public HTTP request.
public sealed class AuditWriteRequest
{
    public Guid? ActorUserId { get; init; }

    public string ActorRole { get; init; } = string.Empty;

    public string Module { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public Guid? EntityId { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? ReasonCode { get; init; }

    public Guid? CorrelationId { get; init; }
}
