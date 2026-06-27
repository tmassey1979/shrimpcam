using NSubstitute;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras.Windows;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class WindowsMediaFoundationDeviceEnumeratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Enumerator_prefers_native_media_foundation_devices_when_available()
    {
        var nativeDevices = new[]
        {
            new MediaFoundationDeviceDescriptor(
                "Logi Native Camera",
                @"\\?\usb#vid_046d&pid_0825#native",
                [new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg)]),
        };
        var nativeDiscovery = new FakeNativeMediaFoundationDeviceDiscovery(nativeDevices);
        var discovery = Substitute.For<IWindowsCameraDiscovery>();
        var enumerator = new WindowsMediaFoundationDeviceEnumerator(nativeDiscovery, discovery);

        var devices = await enumerator.EnumerateAsync(CancellationToken.None).ConfigureAwait(true);

        devices.Should().BeEquivalentTo(nativeDevices);
        await discovery.DidNotReceiveWithAnyArgs().DiscoverAsync(default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Enumerator_maps_windows_camera_discovery_to_media_foundation_descriptors_when_native_is_empty()
    {
        var nativeDiscovery = new FakeNativeMediaFoundationDeviceDiscovery([]);
        var discovery = Substitute.For<IWindowsCameraDiscovery>();
        discovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns(
                [
                    new CameraDescriptor("Logi C270 HD WebCam", @"@device:pnp:\\?\usb#vid_046d&pid_0825", CameraPlatforms.Windows),
                    new CameraDescriptor("Linux camera", "/dev/video0", CameraPlatforms.Linux),
                ]);
        var enumerator = new WindowsMediaFoundationDeviceEnumerator(nativeDiscovery, discovery);

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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Enumerator_falls_back_to_windows_camera_discovery_when_native_fails()
    {
        var nativeDiscovery = new FailingNativeMediaFoundationDeviceDiscovery();
        var discovery = Substitute.For<IWindowsCameraDiscovery>();
        discovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns([new CameraDescriptor("Fallback Camera", "fallback-device", CameraPlatforms.Windows)]);
        var enumerator = new WindowsMediaFoundationDeviceEnumerator(nativeDiscovery, discovery);

        var devices = await enumerator.EnumerateAsync(CancellationToken.None).ConfigureAwait(true);

        devices.Should().ContainSingle();
        devices[0].DisplayName.Should().Be("Fallback Camera");
        devices[0].SymbolicLink.Should().Be("fallback-device");
    }

    private sealed class FakeNativeMediaFoundationDeviceDiscovery(
        IReadOnlyList<MediaFoundationDeviceDescriptor> devices) : IMediaFoundationNativeDeviceDiscovery
    {
        public Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult(devices);
    }

    private sealed class FailingNativeMediaFoundationDeviceDiscovery : IMediaFoundationNativeDeviceDiscovery
    {
        public Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Native Media Foundation unavailable.");
    }
}
