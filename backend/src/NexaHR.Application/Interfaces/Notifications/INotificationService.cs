using NexaHR.Application.DTOs.Notifications;

namespace NexaHR.Application.Interfaces.Notifications;

public interface INotificationService
{
    // API-144
    Task<NotificationListResponse?> GetNotificationsAsync(
        Guid actorId,
        NotificationQuery query);

    // API-145
    Task<NotificationOperationResponse> GetNotificationAsync(
        Guid actorId,
        Guid notificationId);

    // API-146
    Task<NotificationOperationResponse> MarkAsReadAsync(
        Guid actorId,
        Guid notificationId);

    // API-147
    Task<NotificationOperationResponse> MarkAllAsReadAsync(
        Guid actorId);
}
