using System.Runtime.InteropServices;
using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsMediaFoundationDeviceEnumerator(
    IMediaFoundationNativeDeviceDiscovery nativeDeviceDiscovery,
    IWindowsCameraDiscovery cameraDiscovery) : IMediaFoundationDeviceEnumerator
{
    internal static readonly IReadOnlyList<MediaFoundationFrameFormat> DefaultLogitechFormats =
    [
        new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(640, 480, 30, MediaFoundationFormatSubtypes.Mjpeg),
        new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Nv12),
    ];

    public async Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var nativeDevices = await nativeDeviceDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            if (nativeDevices.Count > 0)
            {
                return nativeDevices;
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (COMException)
        {
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return await EnumerateDirectShowFallbackAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> EnumerateDirectShowFallbackAsync(CancellationToken cancellationToken)
    {
        var cameras = await cameraDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var videoCameras = cameras
            .Where(camera => string.Equals(camera.Platform, CameraPlatforms.Windows, StringComparison.OrdinalIgnoreCase))
            .Where(IsVideoCamera)
            .ToList();

        return videoCameras
            .Select((camera, index) => new MediaFoundationDeviceDescriptor(
                camera.DisplayName,
                camera.DevicePath,
                DefaultLogitechFormats,
                index))
            .ToList();
    }

    private static bool IsVideoCamera(CameraDescriptor camera) =>
        !camera.DisplayName.Contains("microphone", StringComparison.OrdinalIgnoreCase)
        && !camera.DevicePath.Contains("wave_", StringComparison.OrdinalIgnoreCase)
        && !camera.DevicePath.Contains("audio", StringComparison.OrdinalIgnoreCase);
}
