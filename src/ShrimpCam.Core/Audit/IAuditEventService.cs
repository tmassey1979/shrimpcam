using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Audit;

public interface IAuditEventService
{
    Task<AuditRecord> RecordAsync(AuditEventRequest request, CancellationToken cancellationToken);
}
