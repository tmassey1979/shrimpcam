using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsMediaFoundationFrameSourceProvider : ICameraFrameSourceProvider
{
    public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
        CameraFrameProviderKinds.WindowsMediaFoundation,
        "Windows Media Foundation Logitech USB adapter",
        CameraPlatforms.Windows,
        IsPrimary: true,
        RequiresExternalProcess: false,
        "windows-media-foundation");
}
