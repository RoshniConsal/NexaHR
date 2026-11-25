namespace NexaHR.Application.DTOs.Notifications;

public sealed class NotificationResponse
{
    public Guid NotificationId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? DeepLink { get; init; }

    public bool IsRead { get; init; }

    public DateTime? ReadAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed class NotificationListResponse
{
    public IReadOnlyList<NotificationResponse> Items
        { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int UnreadCount { get; init; }
}

public sealed class NotificationOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public int UpdatedCount { get; init; }

    public NotificationResponse? Notification { get; init; }
}
