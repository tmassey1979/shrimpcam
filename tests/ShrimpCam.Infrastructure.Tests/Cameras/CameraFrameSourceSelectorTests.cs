using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraFrameSourceSelectorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Automatic_mode_selects_windows_media_foundation_on_windows()
    {
        var selector = CreateSelector();

        var selection = selector.ChooseFrameSource(
            new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                BackendMode = CameraBackendModes.Automatic,
                Source = "Logi C270 HD WebCam",
            },
            CameraPlatforms.Windows);

        selection.ProviderKind.Should().Be(CameraFrameProviderKinds.WindowsMediaFoundation);
        selection.BackendMode.Should().Be(CameraBackendModes.Automatic);
        selection.Platform.Should().Be(CameraPlatforms.Windows);
        selection.IsFallback.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Automatic_mode_selects_linux_v4l2_ffmpeg_on_linux()
    {
        var selector = CreateSelector();

        var selection = selector.ChooseFrameSource(
            new CameraOptions
            {
                Platform = CameraPlatforms.Linux,
                BackendMode = CameraBackendModes.Automatic,
                Source = "/dev/video0",
            },
            CameraPlatforms.Linux);

        selection.ProviderKind.Should().Be(CameraFrameProviderKinds.LinuxV4l2Ffmpeg);
        selection.BackendMode.Should().Be(CameraBackendModes.Automatic);
        selection.Platform.Should().Be(CameraPlatforms.Linux);
        selection.IsFallback.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Explicit_windows_ffmpeg_backend_is_marked_as_fallback()
    {
        var selector = CreateSelector();

        var selection = selector.ChooseFrameSource(
            new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                BackendMode = CameraBackendModes.WindowsFfmpegFallback,
                Source = "Logi C270 HD WebCam",
            },
            CameraPlatforms.Windows);

        selection.ProviderKind.Should().Be(CameraFrameProviderKinds.WindowsFfmpegDirectShow);
        selection.BackendMode.Should().Be(CameraBackendModes.WindowsFfmpegFallback);
        selection.IsFallback.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Backend_mode_must_match_selected_platform()
    {
        var selector = CreateSelector();

        var act = () => selector.ChooseFrameSource(
            new CameraOptions
            {
                Platform = CameraPlatforms.Linux,
                BackendMode = CameraBackendModes.WindowsMediaFoundation,
                Source = "/dev/video0",
            },
            CameraPlatforms.Linux);

        act.Should().Throw<ValidationException>().WithMessage("*requires platform 'Windows'*");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_camera_frame_source_selector()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICameraFrameSourceSelector>().Should().NotBeNull();
    }

    private static ICameraFrameSourceSelector CreateSelector()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICameraFrameSourceSelector>();
    }
}
