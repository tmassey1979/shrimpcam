using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class NativeMediaFoundationCamera : IMediaFoundationCamera
{
    public Task RunAsync(
        CameraOptions options,
        MediaFoundationDeviceDescriptor device,
        MediaFoundationFrameFormat format,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken) =>
        throw new PlatformNotSupportedException(
            "Windows Media Foundation native frame capture is not implemented yet. Use the Windows FFmpeg fallback backend until the native boundary is completed.");
}
