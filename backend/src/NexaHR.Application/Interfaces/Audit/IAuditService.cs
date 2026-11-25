using NexaHR.Application.DTOs.Audit;

namespace NexaHR.Application.Interfaces.Audit;

public interface IAuditService
{
    // API-152: Authorized filtered audit events
    Task<AuditOperationResponse<AuditEventListResponse>>
        GetAuditEventsAsync(
            Guid actorId,
            AuditEventQuery query);

    // API-152: Authorized CSV export
    Task<AuditOperationResponse<AuditExportResponse>>
        ExportAuditEventsAsync(
            Guid actorId,
            AuditEventQuery query);

    // API-153: Authorized audit event detail
    Task<AuditOperationResponse<AuditEventResponse>>
        GetAuditEventAsync(
            Guid actorId,
            Guid eventId);
}
