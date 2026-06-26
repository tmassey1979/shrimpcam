using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraLiveStreamService(ISharedCameraStreamHub streamHub) : ICameraLiveStreamService
{
    public Task<CameraLiveStreamStartResult> StartAsync(
        CameraOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return streamHub.SubscribeAsync(options, cancellationToken);
    }
}
