using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsMediaFoundationDeviceEnumerator(
    IWindowsCameraDiscovery cameraDiscovery) : IMediaFoundationDeviceEnumerator
{
    private static readonly IReadOnlyList<MediaFoundationFrameFormat> DefaultLogitechFormats =
    [
        new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(640, 480, 30, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Nv12),
    ];

    public async Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var cameras = await cameraDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);

        return cameras
            .Where(camera => string.Equals(camera.Platform, CameraPlatforms.Windows, StringComparison.OrdinalIgnoreCase))
            .Select(camera => new MediaFoundationDeviceDescriptor(
                camera.DisplayName,
                camera.DevicePath,
                DefaultLogitechFormats))
            .ToList();
    }
}
