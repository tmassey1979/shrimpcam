using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class MediaFoundationFrameFormatSelector
{
    public static MediaFoundationFrameFormat SelectStreamFormat(
        MediaFoundationDeviceDescriptor device,
        CameraOptions options)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(options);

        var sameResolution = device.Formats
            .Where(format =>
                format.Width == options.StreamWidth
                && format.Height == options.StreamHeight
                && format.FramesPerSecond >= options.StreamFramesPerSecond)
            .OrderBy(format => format.FramesPerSecond)
            .ToArray();
        var mjpegFormat = sameResolution.FirstOrDefault(format => format.IsJpegLike);
        if (mjpegFormat is not null)
        {
            return mjpegFormat;
        }

        var convertibleFormat = sameResolution.FirstOrDefault(format =>
            string.Equals(format.Subtype, MediaFoundationFormatSubtypes.Nv12, StringComparison.OrdinalIgnoreCase)
            || string.Equals(format.Subtype, MediaFoundationFormatSubtypes.Yuy2, StringComparison.OrdinalIgnoreCase));
        if (convertibleFormat is not null)
        {
            return convertibleFormat;
        }

        throw new MediaFoundationUnsupportedFormatException(
            $"Device '{device.DisplayName}' does not expose {options.StreamWidth}x{options.StreamHeight} at {options.StreamFramesPerSecond} FPS in MJPEG, NV12, or YUY2.");
    }
}
