namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal interface IMediaFoundationDeviceEnumerator
{
    Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken);
}
