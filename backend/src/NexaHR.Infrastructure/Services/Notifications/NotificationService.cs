using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Notifications;
using NexaHR.Application.Interfaces.Notifications;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<bool> IsAuthorizedAsync(Guid actorId)
    {
        var user = await _users.FindByIdAsync(actorId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return false;

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
            (roles.Contains(AppRoles.CEO) ||
             roles.Contains(AppRoles.HRManager) ||
             roles.Contains(AppRoles.Candidate) ||
             roles.Contains(AppRoles.Employee));
    }

    private IQueryable<Notification> OwnNotifications(Guid actorId)
    {
        return _db.Notifications
            // Target-specific notifications remain hidden
            // until target authorization is integrated.
            .Where(n =>
                n.RecipientUserId == actorId &&
                n.TargetType == null &&
                n.TargetId == null);
    }

    private static NotificationResponse Map(Notification notification)
    {
        return new NotificationResponse
        {
            NotificationId = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            DeepLink = null,
            IsRead = notification.IsRead,
            ReadAtUtc = notification.ReadAtUtc,
            CreatedAtUtc = notification.CreatedAtUtc
        };
    }

    private static NotificationOperationResponse Fail(
        string code,
        string message)
    {
        return new NotificationOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-144: Own notifications and unread count
    public async Task<NotificationListResponse?> GetNotificationsAsync(
        Guid actorId,
        NotificationQuery request)
    {
        if (!await IsAuthorizedAsync(actorId))
            return null;

        var own = OwnNotifications(actorId).AsNoTracking();

        var unreadCount = await own
            .CountAsync(n => !n.IsRead);

        var query = own;

        if (request.UnreadOnly)
            query = query.Where(n => !n.IsRead);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();

            query = query.Where(n => n.Type == type);
        }

        var page = Math.Clamp(request.Page, 1, 100000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var totalCount = await query.CountAsync();

        var notifications = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new NotificationListResponse
        {
            Items = notifications.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };
    }

    // API-145: Own notification details
    public async Task<NotificationOperationResponse> GetNotificationAsync(
        Guid actorId,
        Guid notificationId)
    {
        if (!await IsAuthorizedAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var notification = await OwnNotifications(actorId)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification is null)
            return Fail("not_found", "Notification not found.");

        return new NotificationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "Notification retrieved successfully.",
            Notification = Map(notification)
        };
    }

    // API-146: Mark own notification read
    public async Task<NotificationOperationResponse> MarkAsReadAsync(
        Guid actorId,
        Guid notificationId)
    {
        if (!await IsAuthorizedAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var now = DateTime.UtcNow;

        var updatedCount = await OwnNotifications(actorId)
            .Where(n => n.Id == notificationId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, (DateTime?)now));

        var notification = await OwnNotifications(actorId)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification is null)
            return Fail("not_found", "Notification not found.");

        return new NotificationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = updatedCount == 0
                ? "Notification is already read."
                : "Notification marked as read.",
            UpdatedCount = updatedCount,
            Notification = Map(notification)
        };
    }

    // API-147: Mark all own notifications read
    public async Task<NotificationOperationResponse> MarkAllAsReadAsync(
        Guid actorId)
    {
        if (!await IsAuthorizedAsync(actorId))
            return Fail("forbidden", "Access denied.");

        var now = DateTime.UtcNow;

        var updatedCount = await OwnNotifications(actorId)
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, (DateTime?)now));

        return new NotificationOperationResponse
        {
            Success = true,
            Code = "success",
            Message = "All own notifications marked as read.",
            UpdatedCount = updatedCount
        };
    }
}
