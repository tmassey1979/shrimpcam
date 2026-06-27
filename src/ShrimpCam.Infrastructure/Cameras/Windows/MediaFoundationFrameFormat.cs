namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed record MediaFoundationFrameFormat(
    int Width,
    int Height,
    int FramesPerSecond,
    string Subtype)
{
    public bool IsJpegLike =>
        string.Equals(Subtype, MediaFoundationFormatSubtypes.Mjpeg, StringComparison.OrdinalIgnoreCase);
}
