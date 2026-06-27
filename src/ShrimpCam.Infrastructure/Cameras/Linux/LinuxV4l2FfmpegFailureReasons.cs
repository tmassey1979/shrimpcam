namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal static class LinuxV4l2FfmpegFailureReasons
{
    public const string MissingDevice = "linuxV4l2DeviceMissing";

    public const string ProcessExited = "linuxV4l2FfmpegProcessExited";

    public const string StartupFailed = "linuxV4l2FfmpegStartupFailed";

    public const string PermissionDenied = "linuxV4l2DevicePermissionDenied";

    public const string UnsupportedResolution = "linuxV4l2UnsupportedResolution";
}
