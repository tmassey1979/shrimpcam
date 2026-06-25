namespace ShrimpCam.Core.Cameras;

public interface ILinuxCameraDiscovery
{
    Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken);
}
