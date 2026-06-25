namespace ShrimpCam.Core.Cameras;

public interface IWindowsCameraDiscovery
{
    Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken);
}
