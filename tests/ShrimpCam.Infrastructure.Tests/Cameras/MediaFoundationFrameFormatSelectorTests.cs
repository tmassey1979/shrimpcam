using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Cameras.Windows;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class MediaFoundationFrameFormatSelectorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Selector_prefers_matching_mjpeg_stream_format()
    {
        var device = new MediaFoundationDeviceDescriptor(
            "Logitech C270 HD WebCam",
            @"\\?\usb#vid_046d&pid_0825",
            [
                new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Nv12),
                new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg),
            ]);

        var format = MediaFoundationFrameFormatSelector.SelectStreamFormat(device, CreateOptions());

        format.Subtype.Should().Be(MediaFoundationFormatSubtypes.Mjpeg);
        format.FramesPerSecond.Should().Be(15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Selector_allows_same_resolution_convertible_format_when_mjpeg_is_unavailable()
    {
        var device = new MediaFoundationDeviceDescriptor(
            "Logitech C920",
            @"\\?\usb#vid_046d&pid_082d",
            [
                new MediaFoundationFrameFormat(1280, 720, 30, MediaFoundationFormatSubtypes.Nv12),
                new MediaFoundationFrameFormat(1920, 1080, 30, MediaFoundationFormatSubtypes.Mjpeg),
            ]);

        var format = MediaFoundationFrameFormatSelector.SelectStreamFormat(device, CreateOptions());

        format.Subtype.Should().Be(MediaFoundationFormatSubtypes.Nv12);
        format.Width.Should().Be(1280);
        format.Height.Should().Be(720);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Selector_rejects_unsupported_media_foundation_formats_with_actionable_message()
    {
        var device = new MediaFoundationDeviceDescriptor(
            "Generic Webcam",
            @"\\?\usb#vid_9999",
            [
                new MediaFoundationFrameFormat(640, 480, 30, "RGB24"),
                new MediaFoundationFrameFormat(1920, 1080, 30, MediaFoundationFormatSubtypes.Mjpeg),
            ]);

        var act = () => MediaFoundationFrameFormatSelector.SelectStreamFormat(device, CreateOptions());

        act.Should().Throw<MediaFoundationUnsupportedFormatException>()
            .WithMessage("*Generic Webcam*1280x720*15 FPS*MJPEG*NV12*YUY2*");
    }

    private static CameraOptions CreateOptions() =>
        new()
        {
            StreamWidth = 1280,
            StreamHeight = 720,
            StreamFramesPerSecond = 15,
        };
}
