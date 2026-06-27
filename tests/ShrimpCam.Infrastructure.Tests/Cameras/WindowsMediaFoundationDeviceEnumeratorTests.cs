using NSubstitute;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras.Windows;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class WindowsMediaFoundationDeviceEnumeratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Enumerator_maps_windows_camera_discovery_to_media_foundation_descriptors()
    {
        var discovery = Substitute.For<IWindowsCameraDiscovery>();
        discovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns(
                [
                    new CameraDescriptor("Logi C270 HD WebCam", @"@device:pnp:\\?\usb#vid_046d&pid_0825", CameraPlatforms.Windows),
                    new CameraDescriptor("Linux camera", "/dev/video0", CameraPlatforms.Linux),
                ]);
        var enumerator = new WindowsMediaFoundationDeviceEnumerator(discovery);

        var devices = await enumerator.EnumerateAsync(CancellationToken.None).ConfigureAwait(true);

        devices.Should().ContainSingle();
        devices[0].DisplayName.Should().Be("Logi C270 HD WebCam");
        devices[0].SymbolicLink.Should().Be(@"@device:pnp:\\?\usb#vid_046d&pid_0825");
        devices[0].Formats.Should().Contain(format =>
            format.Width == 1280
            && format.Height == 720
            && format.FramesPerSecond == 15
            && format.Subtype == MediaFoundationFormatSubtypes.Mjpeg);
    }
}
