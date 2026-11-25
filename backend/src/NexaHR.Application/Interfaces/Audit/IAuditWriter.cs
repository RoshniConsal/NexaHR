using NexaHR.Application.DTOs.Audit;

namespace NexaHR.Application.Interfaces.Audit;

public interface IAuditWriter
{
    // Stage the audit event in the shared DbContext.
    // The calling business operation must commit it
    // together with the business changes.
    // Actor identity must come from trusted server context.
    Guid StageEvent(AuditWriteRequest request);
}
