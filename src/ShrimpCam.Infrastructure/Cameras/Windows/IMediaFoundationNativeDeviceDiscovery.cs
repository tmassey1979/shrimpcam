namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal interface IMediaFoundationNativeDeviceDiscovery
{
    Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken);
}
