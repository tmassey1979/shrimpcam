using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Cameras;
using ShrimpCam.Infrastructure.Cameras.Linux;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class LinuxV4l2FfmpegFrameSourceAdapterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Start_publishes_ffmpeg_v4l2_frames_to_latest_frame_cache()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var command = new ProcessRequest("ffmpeg", "-f video4linux2");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        var frame = new byte[] { 0xFF, 0xD8, 0x40, 0x41, 0xFF, 0xD9 };
        var options = CreateOptions(reconnectRetryAttempts: 0);

        commandFactory.BuildLiveStreamCommand(options).Returns(command);
        processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>())
            .Returns(new StubProcessStream(new MemoryStream(frame), new ProcessResult(1, string.Empty, "camera unplugged")));

        var adapter = new LinuxV4l2FfmpegFrameSourceAdapter(commandFactory, processStreamRunner, cameraStatusService, frameStore);

        try
        {
            var result = adapter.Start(options, CancellationToken.None);
            await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

            result.Succeeded.Should().BeTrue();
            (await frameStore.TryWriteLatestFrameAsync(outputPath, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .BeTrue();
            File.ReadAllBytes(outputPath).Should().Equal(frame);
            cameraStatusService.Received(1).ReportOnline();
            cameraStatusService.Received(1)
                .ReportDegraded(Arg.Is<string>(reason =>
                    reason.Contains(LinuxV4l2FfmpegFailureReasons.ProcessExited, StringComparison.Ordinal)
                    && reason.Contains("camera unplugged", StringComparison.Ordinal)));
        }
        finally
        {
            DeleteIfExists(outputPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Exited_ffmpeg_v4l2_process_follows_configured_reconnect_policy()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var command = new ProcessRequest("ffmpeg", "-f video4linux2");
        var options = CreateOptions(reconnectRetryAttempts: 1, reconnectBackoffSeconds: 0);
        var handles = new Queue<IProcessStreamHandle>(
            [
                new StubProcessStream(new MemoryStream([]), new ProcessResult(1, string.Empty, "first exit")),
                new StubProcessStream(new MemoryStream([]), new ProcessResult(1, string.Empty, "second exit")),
            ]);

        commandFactory.BuildLiveStreamCommand(options).Returns(command);
        processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(handles.Dequeue()));

        var adapter = new LinuxV4l2FfmpegFrameSourceAdapter(commandFactory, processStreamRunner, cameraStatusService, frameStore);

        var result = adapter.Start(options, CancellationToken.None);
        await result.RunningTask!.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        await processStreamRunner.Received(2).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        cameraStatusService.Received(2).ReportDegraded(Arg.Is<string>(reason =>
            reason.Contains(LinuxV4l2FfmpegFailureReasons.ProcessExited, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Missing_v4l2_device_fails_before_starting_ffmpeg()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var adapter = new LinuxV4l2FfmpegFrameSourceAdapter(commandFactory, processStreamRunner, cameraStatusService, frameStore);

        var result = adapter.Start(CreateOptions(source: "video0"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(LinuxV4l2FfmpegFailureReasons.MissingDevice);
        commandFactory.DidNotReceiveWithAnyArgs().BuildLiveStreamCommand(default!);
        await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);
        cameraStatusService.Received(1).ReportDegraded(LinuxV4l2FfmpegFailureReasons.MissingDevice);
    }

    private static CameraOptions CreateOptions(
        string source = "/dev/video0",
        int reconnectRetryAttempts = 2,
        int reconnectBackoffSeconds = 1) =>
        new()
        {
            Platform = CameraPlatforms.Linux,
            Source = source,
            BackendMode = CameraBackendModes.LinuxV4l2Ffmpeg,
            StreamWidth = 1280,
            StreamHeight = 720,
            StreamFramesPerSecond = 15,
            ReconnectRetryAttempts = reconnectRetryAttempts,
            ReconnectBackoffSeconds = reconnectBackoffSeconds,
        };

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class StubProcessStream(Stream standardOutput, ProcessResult exitResult) : IProcessStreamHandle
    {
        public Stream StandardOutput { get; } = standardOutput;

        public Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(exitResult);
        }

        public ValueTask DisposeAsync() => StandardOutput.DisposeAsync();
    }
}
