using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal interface IMediaFoundationCamera
{
    Task RunAsync(
        CameraOptions options,
        MediaFoundationDeviceDescriptor device,
        MediaFoundationFrameFormat format,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken);
}
