namespace NexaHR.Application.DTOs.Departments;

public sealed class DepartmentResponse
{
    public Guid DepartmentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsArchived { get; init; }

    public DateTime? CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime? ArchivedAtUtc { get; init; }

    public string? RowVersion { get; init; }
}

public sealed class DepartmentListResponse
{
    public IReadOnlyList<DepartmentResponse> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class DepartmentOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DepartmentResponse? Department { get; init; }
}
