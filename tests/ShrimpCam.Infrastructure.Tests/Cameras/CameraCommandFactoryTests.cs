using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraCommandFactoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Discovery_command_targets_linux_workflow()
    {
        var factory = BuildFactory();

        var command = factory.BuildDiscoveryCommand(CameraPlatforms.Linux);

        command.FileName.Should().Be("v4l2-ctl");
        command.Arguments.Should().Be("--list-devices");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Discovery_command_targets_windows_workflow()
    {
        var factory = BuildFactory();

        var command = factory.BuildDiscoveryCommand(CameraPlatforms.Windows);

        command.FileName.Should().Be("ffmpeg");
        command.Arguments.Should().Contain("-list_devices true");
        command.Arguments.Should().Contain("-f dshow");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Linux_still_capture_command_uses_configured_device_and_resolution()
    {
        var factory = BuildFactory();
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Linux,
            Source = "/dev/video2",
            CaptureWidth = 2592,
            CaptureHeight = 1944,
        };

        var command = factory.BuildStillCaptureCommand(options, "captures/latest.jpg");

        command.FileName.Should().Be("ffmpeg");
        command.Arguments.Should().Contain("-f video4linux2");
        command.Arguments.Should().Contain("-video_size 2592x1944");
        command.Arguments.Should().Contain("-i \"/dev/video2\"");
        command.Arguments.Should().Contain("\"captures/latest.jpg\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_stream_command_uses_configured_identifier_resolution_and_frame_rate()
    {
        var factory = BuildFactory();
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Windows,
            Source = "@device_pnp_\\\\?\\usb#vid_046d",
            StreamWidth = 1920,
            StreamHeight = 1080,
            StreamFramesPerSecond = 24,
        };

        var command = factory.BuildLiveStreamCommand(options);

        command.FileName.Should().Be("ffmpeg");
        command.Arguments.Should().Contain("-f dshow");
        command.Arguments.Should().Contain("-framerate 24");
        command.Arguments.Should().Contain("-video_size 1920x1080");
        command.Arguments.Should().Contain("-i video=\"@device_pnp_\\\\\\\\?\\\\usb#vid_046d\"");
        command.Arguments.Should().Contain("-f mpjpeg");
        command.Arguments.Should().Contain("-boundary_tag shrimpcam");
        command.Arguments.Should().Contain("pipe:1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_source_identifiers_with_quotes_are_escaped()
    {
        var factory = BuildFactory();
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Windows,
            Source = "Logitech \"BRIO\"",
        };

        var command = factory.BuildStillCaptureCommand(options, "captures/latest.jpg");

        command.Arguments.Should().Contain("-i video=\"Logitech \\\"BRIO\\\"\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Invalid_camera_configuration_is_rejected_before_command_generation()
    {
        var factory = BuildFactory();
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Windows,
            Source = string.Empty,
        };

        var act = () => factory.BuildLiveStreamCommand(options);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_camera_command_factory()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICameraCommandFactory>().Should().NotBeNull();
    }

    private static ICameraCommandFactory BuildFactory()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICameraCommandFactory>();
    }
}
