using NexaHR.Application.DTOs.Dashboard;

namespace NexaHR.Application.Interfaces.Dashboard;

public interface IDashboardService
{
    // API-148: Role-specific dashboard
    Task<DashboardResponse?> GetDashboardAsync(
        Guid actorId);

    // API-149: Authorized pending actions
    Task<PendingActionsResponse?> GetPendingActionsAsync(
        Guid actorId);
}
