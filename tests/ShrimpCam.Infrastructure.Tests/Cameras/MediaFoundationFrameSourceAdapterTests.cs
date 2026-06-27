using NSubstitute;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Cameras;
using ShrimpCam.Infrastructure.Cameras.Windows;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class MediaFoundationFrameSourceAdapterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Start_publishes_media_foundation_frames_to_latest_frame_cache()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var camera = new FakeMediaFoundationCamera(async (onFrame, cancellationToken) =>
        {
            await onFrame(new byte[] { 0xFF, 0xD8, 0x12, 0x34, 0xFF, 0xD9 }, cancellationToken).ConfigureAwait(false);
        });
        var adapter = new MediaFoundationFrameSourceAdapter(camera, cameraStatusService, frameStore);
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            var result = adapter.Start(CreateOptions(), CancellationToken.None);
            await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

            result.Succeeded.Should().BeTrue();
            (await frameStore.TryWriteLatestFrameAsync(outputPath, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .BeTrue();
            File.ReadAllBytes(outputPath).Should().Equal([0xFF, 0xD8, 0x12, 0x34, 0xFF, 0xD9]);
            cameraStatusService.Received(1).ReportOnline();
        }
        finally
        {
            DeleteIfExists(outputPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Unsupported_media_foundation_format_reports_actionable_degraded_health()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var camera = new FakeMediaFoundationCamera((_, _) =>
            throw new MediaFoundationUnsupportedFormatException("NV12 800x600 is not configured"));
        var adapter = new MediaFoundationFrameSourceAdapter(camera, cameraStatusService, frameStore);

        var result = adapter.Start(CreateOptions(), CancellationToken.None);
        await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        cameraStatusService.Received(1)
            .ReportDegraded(Arg.Is<string>(reason =>
                reason.Contains(MediaFoundationFailureReasons.UnsupportedFormat, StringComparison.Ordinal)
                && reason.Contains("NV12 800x600", StringComparison.Ordinal)));
        cameraStatusService.DidNotReceive().ReportOnline();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Missing_media_foundation_device_fails_before_starting_camera_boundary()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var camera = new FakeMediaFoundationCamera((_, _) => Task.CompletedTask);
        var adapter = new MediaFoundationFrameSourceAdapter(camera, cameraStatusService, frameStore);

        var result = adapter.Start(
            new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                Source = string.Empty,
                BackendMode = CameraBackendModes.WindowsMediaFoundation,
            },
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(MediaFoundationFailureReasons.MissingDevice);
        camera.StartCount.Should().Be(0);
        cameraStatusService.Received(1).ReportDegraded(MediaFoundationFailureReasons.MissingDevice);
    }

    private static CameraOptions CreateOptions() =>
        new()
        {
            Platform = CameraPlatforms.Windows,
            Source = "Logi C270 HD WebCam",
            BackendMode = CameraBackendModes.WindowsMediaFoundation,
            StreamWidth = 1280,
            StreamHeight = 720,
            StreamFramesPerSecond = 15,
        };

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class FakeMediaFoundationCamera(
        Func<Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, CancellationToken, Task> run) : IMediaFoundationCamera
    {
        public int StartCount { get; private set; }

        public Task RunAsync(
            CameraOptions options,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
            CancellationToken cancellationToken)
        {
            StartCount++;
            return run(onFrame, cancellationToken);
        }
    }
}
