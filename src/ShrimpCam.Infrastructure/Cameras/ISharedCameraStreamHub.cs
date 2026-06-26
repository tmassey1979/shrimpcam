using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal interface ISharedCameraStreamHub
{
    Task<CameraLiveStreamStartResult> SubscribeAsync(CameraOptions options, CancellationToken cancellationToken);

    Task EnsureRunningAsync(CameraOptions options, CancellationToken cancellationToken);
}
