namespace NexaHR.Application.DTOs.Audit;

public sealed class AuditEventResponse
{
    public Guid EventId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string ActorRole { get; init; } = string.Empty;

    public string Module { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public Guid? EntityId { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? ReasonCode { get; init; }

    public Guid? CorrelationId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public bool IsRedacted { get; init; }
}

public sealed class AuditEventListResponse
{
    public IReadOnlyList<AuditEventResponse> Items
        { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class AuditExportResponse
{
    public byte[] Content { get; init; } = [];

    public string ContentType { get; init; }
        = "text/csv; charset=utf-8";

    public string FileName { get; init; }
        = "audit-events.csv";
}

public sealed class AuditOperationResponse<T>
    where T : class
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }
}
