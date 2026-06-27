using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal interface IMediaFoundationCamera
{
    Task RunAsync(
        CameraOptions options,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken);
}
