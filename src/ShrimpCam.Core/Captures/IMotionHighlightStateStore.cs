using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IMotionHighlightStateStore
{
    Task<MotionHighlightState> LoadAsync(
        StorageOptions options,
        CancellationToken cancellationToken);

    Task SaveAsync(
        StorageOptions options,
        MotionHighlightState state,
        CancellationToken cancellationToken);
}
