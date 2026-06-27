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
        var device = CreateDevice();
        var deviceEnumerator = new FakeMediaFoundationDeviceEnumerator([device]);
        var camera = new FakeMediaFoundationCamera(async (onFrame, cancellationToken) =>
        {
            await onFrame(new byte[] { 0xFF, 0xD8, 0x12, 0x34, 0xFF, 0xD9 }, cancellationToken).ConfigureAwait(false);
        });
        var adapter = new MediaFoundationFrameSourceAdapter(deviceEnumerator, camera, cameraStatusService, frameStore);
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
            camera.SelectedDevice.Should().Be(device);
            camera.SelectedFormat.Should().Be(new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg));
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
        var deviceEnumerator = new FakeMediaFoundationDeviceEnumerator(
            [
                CreateDevice(
                    [
                        new MediaFoundationFrameFormat(800, 600, 30, MediaFoundationFormatSubtypes.Nv12),
                    ]),
            ]);
        var camera = new FakeMediaFoundationCamera((_, _) => Task.CompletedTask);
        var adapter = new MediaFoundationFrameSourceAdapter(deviceEnumerator, camera, cameraStatusService, frameStore);

        var result = adapter.Start(CreateOptions(), CancellationToken.None);
        await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        cameraStatusService.Received(1)
            .ReportDegraded(Arg.Is<string>(reason =>
                reason.Contains(MediaFoundationFailureReasons.UnsupportedFormat, StringComparison.Ordinal)
                && reason.Contains("1280x720", StringComparison.Ordinal)));
        cameraStatusService.DidNotReceive().ReportOnline();
        camera.StartCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Missing_media_foundation_device_fails_before_starting_camera_boundary()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var deviceEnumerator = new FakeMediaFoundationDeviceEnumerator([]);
        var camera = new FakeMediaFoundationCamera((_, _) => Task.CompletedTask);
        var adapter = new MediaFoundationFrameSourceAdapter(deviceEnumerator, camera, cameraStatusService, frameStore);

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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Missing_configured_media_foundation_device_reports_degraded_without_starting_camera()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var deviceEnumerator = new FakeMediaFoundationDeviceEnumerator([CreateDevice()]);
        var camera = new FakeMediaFoundationCamera((_, _) => Task.CompletedTask);
        var adapter = new MediaFoundationFrameSourceAdapter(deviceEnumerator, camera, cameraStatusService, frameStore);

        var result = adapter.Start(CreateOptions("Missing Camera"), CancellationToken.None);
        await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        camera.StartCount.Should().Be(0);
        cameraStatusService.Received(1).ReportDegraded(MediaFoundationFailureReasons.MissingDevice);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Windows_media_foundation_provider_delegates_to_adapter_and_publishes_frames()
    {
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var deviceEnumerator = new FakeMediaFoundationDeviceEnumerator([CreateDevice()]);
        var camera = new FakeMediaFoundationCamera(async (onFrame, cancellationToken) =>
        {
            await onFrame(new byte[] { 0xFF, 0xD8, 0x55, 0xAA, 0xFF, 0xD9 }, cancellationToken).ConfigureAwait(false);
        });
        var adapter = new MediaFoundationFrameSourceAdapter(deviceEnumerator, camera, cameraStatusService, frameStore);
        var provider = new WindowsMediaFoundationFrameSourceProvider(adapter);
        var published = new List<byte[]>();

        var result = provider.Start(CreateOptions(), frame => published.Add(frame.ToArray()), CancellationToken.None);
        await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        published.Should().ContainSingle()
            .Which.Should().Equal([0xFF, 0xD8, 0x55, 0xAA, 0xFF, 0xD9]);
        cameraStatusService.Received(1).ReportOnline();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_media_foundation_provider_reports_runtime_available()
    {
        var adapter = new MediaFoundationFrameSourceAdapter(
            new FakeMediaFoundationDeviceEnumerator([]),
            new FakeMediaFoundationCamera((_, _) => Task.CompletedTask),
            Substitute.For<ICameraStatusService>(),
            new LiveFrameSnapshotStore());
        var provider = new WindowsMediaFoundationFrameSourceProvider(adapter);

        provider.Descriptor.IsRuntimeAvailable.Should().BeTrue();
        provider.Descriptor.UnavailableReason.Should().BeNull();
        provider.Descriptor.RequiresExternalProcess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Native_media_foundation_camera_requires_native_device_index()
    {
        var camera = new NativeMediaFoundationCamera();
        var act = async () => await camera
            .RunAsync(
                CreateOptions(),
                CreateDevice(),
                new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .ConfigureAwait(true);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*native camera index*")
            .ConfigureAwait(true);
    }

    private static CameraOptions CreateOptions(string source = "Logi C270 HD WebCam") =>
        new()
        {
            Platform = CameraPlatforms.Windows,
            Source = source,
            BackendMode = CameraBackendModes.WindowsMediaFoundation,
            StreamWidth = 1280,
            StreamHeight = 720,
            StreamFramesPerSecond = 15,
        };

    private static MediaFoundationDeviceDescriptor CreateDevice(
        IReadOnlyList<MediaFoundationFrameFormat>? formats = null) =>
        new(
            "Logi C270 HD WebCam",
            @"\\?\usb#vid_046d&pid_0825",
            formats ??
            [
                new MediaFoundationFrameFormat(1280, 720, 15, MediaFoundationFormatSubtypes.Mjpeg),
            ]);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class FakeMediaFoundationDeviceEnumerator(
        IReadOnlyList<MediaFoundationDeviceDescriptor> devices) : IMediaFoundationDeviceEnumerator
    {
        public Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(devices);
    }

    private sealed class FakeMediaFoundationCamera(
        Func<Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, CancellationToken, Task> run) : IMediaFoundationCamera
    {
        public int StartCount { get; private set; }

        public MediaFoundationDeviceDescriptor? SelectedDevice { get; private set; }

        public MediaFoundationFrameFormat? SelectedFormat { get; private set; }

        public Task RunAsync(
            CameraOptions options,
            MediaFoundationDeviceDescriptor device,
            MediaFoundationFrameFormat format,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
            CancellationToken cancellationToken)
        {
            StartCount++;
            SelectedDevice = device;
            SelectedFormat = format;
            return run(onFrame, cancellationToken);
        }
    }
}
