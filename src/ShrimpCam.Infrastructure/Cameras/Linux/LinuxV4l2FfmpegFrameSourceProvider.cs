using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal sealed class LinuxV4l2FfmpegFrameSourceProvider(
    LinuxV4l2FfmpegFrameSourceAdapter adapter) : ICameraFrameSourceProvider
{
    public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
        CameraFrameProviderKinds.LinuxV4l2Ffmpeg,
        "Linux V4L2 FFmpeg Logitech UVC adapter",
        CameraPlatforms.Linux,
        IsPrimary: true,
        RequiresExternalProcess: true,
        "v4l2-ffmpeg");

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
