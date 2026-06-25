using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IScheduledCaptureStateStore
{
    Task<ScheduledCaptureState> LoadAsync(
        StorageOptions options,
        CancellationToken cancellationToken);

    Task SaveAsync(
        StorageOptions options,
        ScheduledCaptureState state,
        CancellationToken cancellationToken);
}
