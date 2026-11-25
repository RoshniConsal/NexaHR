using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Notifications;

public sealed class NotificationQuery
{
    public bool UnreadOnly { get; set; }

    [StringLength(50)]
    public string? Type { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
