namespace ShrimpCam.Core.Cameras;

public static class CameraBackendModes
{
    public const string Automatic = "Automatic";

    public const string WindowsMediaFoundation = "WindowsMediaFoundation";

    public const string WindowsFfmpegFallback = "WindowsFfmpegFallback";

    public const string LinuxV4l2Ffmpeg = "LinuxV4l2Ffmpeg";
}
