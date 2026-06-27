using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraFrameSourceProviderRegistryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Automatic_windows_backend_resolves_media_foundation_provider()
    {
        var registry = CreateRegistry();

        var provider = registry.GetProvider(
            new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                BackendMode = CameraBackendModes.Automatic,
                Source = "Logi C270 HD WebCam",
            },
            CameraPlatforms.Windows);

        provider.Descriptor.ProviderKind.Should().Be(CameraFrameProviderKinds.WindowsMediaFoundation);
        provider.Descriptor.IsPrimary.Should().BeTrue();
        provider.Descriptor.RequiresExternalProcess.Should().BeFalse();
        provider.Descriptor.IsRuntimeAvailable.Should().BeTrue();
        provider.Descriptor.UnavailableReason.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Explicit_windows_ffmpeg_backend_resolves_fallback_provider()
    {
        var registry = CreateRegistry();

        var provider = registry.GetProvider(
            new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                BackendMode = CameraBackendModes.WindowsFfmpegFallback,
                Source = "Logi C270 HD WebCam",
            },
            CameraPlatforms.Windows);

        provider.Descriptor.ProviderKind.Should().Be(CameraFrameProviderKinds.WindowsFfmpegDirectShow);
        provider.Descriptor.IsPrimary.Should().BeFalse();
        provider.Descriptor.RequiresExternalProcess.Should().BeTrue();
        provider.Descriptor.IsRuntimeAvailable.Should().BeTrue();
        provider.Descriptor.UnavailableReason.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Automatic_linux_backend_resolves_v4l2_ffmpeg_provider()
    {
        var registry = CreateRegistry();

        var provider = registry.GetProvider(
            new CameraOptions
            {
                Platform = CameraPlatforms.Linux,
                BackendMode = CameraBackendModes.Automatic,
                Source = "/dev/video0",
            },
            CameraPlatforms.Linux);

        provider.Descriptor.ProviderKind.Should().Be(CameraFrameProviderKinds.LinuxV4l2Ffmpeg);
        provider.Descriptor.IsPrimary.Should().BeTrue();
        provider.Descriptor.RequiresExternalProcess.Should().BeTrue();
        provider.Descriptor.IsRuntimeAvailable.Should().BeTrue();
        provider.Descriptor.UnavailableReason.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_all_frame_source_providers()
    {
        var registry = CreateRegistry();

        registry.ListProviders()
            .Select(provider => provider.ProviderKind)
            .Should()
            .BeEquivalentTo(
                CameraFrameProviderKinds.WindowsMediaFoundation,
                CameraFrameProviderKinds.WindowsFfmpegDirectShow,
                CameraFrameProviderKinds.LinuxV4l2Ffmpeg);
    }

    private static ICameraFrameSourceProviderRegistry CreateRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICameraFrameSourceProviderRegistry>();
    }
}
