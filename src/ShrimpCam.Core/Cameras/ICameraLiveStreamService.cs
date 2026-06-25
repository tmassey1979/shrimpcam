using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public interface ICameraLiveStreamService
{
    Task<CameraLiveStreamStartResult> StartAsync(
        CameraOptions options,
        CancellationToken cancellationToken);
}
