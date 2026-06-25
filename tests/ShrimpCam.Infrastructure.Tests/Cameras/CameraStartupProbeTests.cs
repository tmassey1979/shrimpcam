using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Cameras;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraStartupProbeTests
{
    [Fact]
    public async Task Startup_probe_reports_online_when_configured_camera_is_discovered()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var linuxDiscovery = Substitute.For<ILinuxCameraDiscovery>();
        var windowsDiscovery = Substitute.For<IWindowsCameraDiscovery>();
        var probe = CreateProbe(linuxDiscovery, windowsDiscovery, cameraStatusService, CameraPlatforms.Linux, "/dev/video0");

        linuxDiscovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns([new CameraDescriptor("Logitech USB Camera", "/dev/video0", CameraPlatforms.Linux)]);

        await probe.CheckAsync(CancellationToken.None).ConfigureAwait(true);

        cameraStatusService.Received(1).ReportOnline();
        cameraStatusService.DidNotReceiveWithAnyArgs().ReportDegraded(default!);
    }

    [Fact]
    public async Task Startup_probe_reports_degraded_when_configured_camera_is_missing()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var linuxDiscovery = Substitute.For<ILinuxCameraDiscovery>();
        var windowsDiscovery = Substitute.For<IWindowsCameraDiscovery>();
        var probe = CreateProbe(linuxDiscovery, windowsDiscovery, cameraStatusService, CameraPlatforms.Linux, "/dev/video404");

        linuxDiscovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns([new CameraDescriptor("Logitech USB Camera", "/dev/video0", CameraPlatforms.Linux)]);

        await probe.CheckAsync(CancellationToken.None).ConfigureAwait(true);

        cameraStatusService.Received(1).ReportDegraded(Arg.Is<string>(reason => reason.Contains("/dev/video404", StringComparison.Ordinal)));
        cameraStatusService.DidNotReceive().ReportOnline();
    }

    [Fact]
    public async Task Startup_probe_reports_degraded_when_camera_discovery_fails()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var linuxDiscovery = Substitute.For<ILinuxCameraDiscovery>();
        var windowsDiscovery = Substitute.For<IWindowsCameraDiscovery>();
        var probe = CreateProbe(linuxDiscovery, windowsDiscovery, cameraStatusService, CameraPlatforms.Linux, "/dev/video0");

        linuxDiscovery.DiscoverAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<CameraDescriptor>>>(_ => throw new InvalidOperationException("camera command missing"));

        await probe.CheckAsync(CancellationToken.None).ConfigureAwait(true);

        cameraStatusService.Received(1).ReportDegraded("camera command missing");
        cameraStatusService.DidNotReceive().ReportOnline();
    }

    private static CameraStartupProbe CreateProbe(
        ILinuxCameraDiscovery linuxDiscovery,
        IWindowsCameraDiscovery windowsDiscovery,
        ICameraStatusService cameraStatusService,
        string platform,
        string source) =>
        new(
            Options.Create(
                new ShrimpCamOptions
                {
                    Camera = new CameraOptions
                    {
                        Platform = platform,
                        Source = source,
                    },
                }),
            linuxDiscovery,
            windowsDiscovery,
            cameraStatusService,
            NullLogger<CameraStartupProbe>.Instance);
}
