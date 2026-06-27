using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsMediaFoundationFrameSourceProvider(
    MediaFoundationFrameSourceAdapter adapter) : ICameraFrameSourceProvider
{
    public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
        CameraFrameProviderKinds.WindowsMediaFoundation,
        "Windows Media Foundation Logitech USB adapter",
        CameraPlatforms.Windows,
        IsPrimary: true,
        RequiresExternalProcess: false,
        "windows-media-foundation",
        IsRuntimeAvailable: false,
        MediaFoundationFailureReasons.NativeBoundaryUnavailable);

    public CameraFrameSourceStartResult Start(
        CameraOptions options,
        Action<ReadOnlyMemory<byte>> publishFrame,
        CancellationToken cancellationToken)
    {
        var result = adapter.Start(options, cancellationToken, publishFrame);
        return result.Succeeded
            ? CameraFrameSourceStartResult.Success(result.RunningTask!)
            : CameraFrameSourceStartResult.Failure(result.FailureReason!);
    }
}
