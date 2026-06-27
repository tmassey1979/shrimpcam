using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal sealed class LinuxV4l2FfmpegFrameSourceProvider : ICameraFrameSourceProvider
{
    public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
        CameraFrameProviderKinds.LinuxV4l2Ffmpeg,
        "Linux V4L2 FFmpeg Logitech UVC adapter",
        CameraPlatforms.Linux,
        IsPrimary: true,
        RequiresExternalProcess: true,
        "v4l2-ffmpeg");
}
