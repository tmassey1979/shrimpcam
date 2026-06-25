namespace ShrimpCam.Core.Persistence;

public interface IAuditRecordRepository
{
    Task CreateAsync(AuditRecord auditRecord, CancellationToken cancellationToken);

    Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
