namespace ShrimpCam.Core.Persistence;

public interface ICaptureRecordRepository
{
    Task CreateAsync(CaptureRecord captureRecord, CancellationToken cancellationToken);

    Task<CaptureRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CaptureRecordPage> ListAsync(CaptureRecordQuery query, CancellationToken cancellationToken);
}
