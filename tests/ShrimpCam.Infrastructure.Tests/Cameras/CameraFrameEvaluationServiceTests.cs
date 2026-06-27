using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Settings;
using ShrimpCam.Infrastructure.Cameras;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraFrameEvaluationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Run_once_resolves_configured_provider_and_keeps_always_on_stream_running()
    {
        var settings = CreateSettings(
            CameraPlatforms.Windows,
            CameraBackendModes.WindowsFfmpegFallback,
            alwaysOnStreamEnabled: true);
        var settingsService = Substitute.For<IEditableSettingsService>();
        var providerRegistry = Substitute.For<ICameraFrameSourceProviderRegistry>();
        var streamHub = new CapturingSharedCameraStreamHub();
        var provider = Substitute.For<ICameraFrameSourceProvider>();

        settingsService.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(settings);
        provider.Descriptor.Returns(new CameraFrameSourceProviderDescriptor(
            CameraFrameProviderKinds.WindowsFfmpegDirectShow,
            "Windows FFmpeg DirectShow fallback",
            CameraPlatforms.Windows,
            IsPrimary: false,
            RequiresExternalProcess: true,
            "windows-ffmpeg-dshow"));
        providerRegistry.GetProvider(settings.Camera, CameraPlatforms.Windows).Returns(provider);
        var service = new CameraFrameEvaluationService(
            settingsService,
            providerRegistry,
            streamHub,
            NullLogger<CameraFrameEvaluationService>.Instance);

        await service.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);

        providerRegistry.Received(1).GetProvider(settings.Camera, CameraPlatforms.Windows);
        streamHub.EnsureRunningCalls.Should().ContainSingle(call => ReferenceEquals(call.Options, settings.Camera));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Run_once_resolves_linux_provider_without_starting_stream_when_always_on_is_disabled()
    {
        var settings = CreateSettings(
            CameraPlatforms.Linux,
            CameraBackendModes.LinuxV4l2Ffmpeg,
            alwaysOnStreamEnabled: false);
        var settingsService = Substitute.For<IEditableSettingsService>();
        var providerRegistry = Substitute.For<ICameraFrameSourceProviderRegistry>();
        var streamHub = new CapturingSharedCameraStreamHub();
        var provider = Substitute.For<ICameraFrameSourceProvider>();

        settingsService.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(settings);
        provider.Descriptor.Returns(new CameraFrameSourceProviderDescriptor(
            CameraFrameProviderKinds.LinuxV4l2Ffmpeg,
            "Linux V4L2 FFmpeg Logitech UVC adapter",
            CameraPlatforms.Linux,
            IsPrimary: true,
            RequiresExternalProcess: true,
            "v4l2-ffmpeg"));
        providerRegistry.GetProvider(settings.Camera, CameraPlatforms.Linux).Returns(provider);
        var service = new CameraFrameEvaluationService(
            settingsService,
            providerRegistry,
            streamHub,
            NullLogger<CameraFrameEvaluationService>.Instance);

        await service.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);

        providerRegistry.Received(1).GetProvider(settings.Camera, CameraPlatforms.Linux);
        streamHub.EnsureRunningCalls.Should().BeEmpty();
    }

    private static EditableSettings CreateSettings(
        string platform,
        string backendMode,
        bool alwaysOnStreamEnabled) =>
        new(
            new CameraOptions
            {
                Platform = platform,
                Source = platform == CameraPlatforms.Linux ? "/dev/video0" : "Logi C270 HD WebCam",
                BackendMode = backendMode,
                StreamWidth = 1280,
                StreamHeight = 720,
                StreamFramesPerSecond = 15,
                AlwaysOnStreamEnabled = alwaysOnStreamEnabled,
            },
            new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 0,
                ActiveEndHourUtc = 24,
            },
            new StorageEditableSettings(30),
            new SecurityOptions
            {
                HostMode = "LocalOnly",
            });

    private sealed class CapturingSharedCameraStreamHub : ISharedCameraStreamHub
    {
        public List<(CameraOptions Options, CancellationToken CancellationToken)> EnsureRunningCalls { get; } = [];

        public Task<CameraLiveStreamStartResult> SubscribeAsync(CameraOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(CameraLiveStreamStartResult.Failure("not used"));

        public Task EnsureRunningAsync(CameraOptions options, CancellationToken cancellationToken)
        {
            EnsureRunningCalls.Add((options, cancellationToken));
            return Task.CompletedTask;
        }
    }
}
