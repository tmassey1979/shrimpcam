using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsFfmpegFallbackFrameSourceProvider : ICameraFrameSourceProvider
{
    public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
        CameraFrameProviderKinds.WindowsFfmpegDirectShow,
        "Windows FFmpeg DirectShow fallback",
        CameraPlatforms.Windows,
        IsPrimary: false,
        RequiresExternalProcess: true,
        "windows-ffmpeg-dshow");

    public CameraFrameSourceStartResult Start(
        CameraOptions options,
        Action<ReadOnlyMemory<byte>> publishFrame,
        CancellationToken cancellationToken) =>
        CameraFrameSourceStartResult.Failure("windowsFfmpegFallbackProviderNotFrameBacked");
}
