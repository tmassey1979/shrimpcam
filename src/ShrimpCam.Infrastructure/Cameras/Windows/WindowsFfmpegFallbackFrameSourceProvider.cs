using ShrimpCam.Core.Cameras;

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
}
